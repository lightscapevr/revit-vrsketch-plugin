using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;


namespace VRSketch
{
    public class VRSketchCommand
    {
        static VRSketchCommand instance;
        public static VRSketchCommand GetInstance()
        {
            if (instance == null)
                instance = new VRSketchCommand();
            return instance;
        }

        Document doc;


        IEnumerable<T> GetElements<T>() where T : Element
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(T));
            return collector.ToElements().Cast<T>();
        }

        static string GetMatName(int mat_id)
        {
            return mat_id.ToString();
        }


        /* DEBUG ONLY: dumps a detailed log of the structure of objects from Revit */
        const string DUMP_FILE = null;   // @"Z:\VRSketch-Revit.log";
        static Stream log_stream;
        static object lock_log_stream = new object();

        [System.Diagnostics.Conditional("DEBUG")]
        public static void _WriteLog(string data)
        {
            if (DUMP_FILE == null)
                return;
            lock (lock_log_stream)
            {
                if (log_stream == null)
                    log_stream = new FileStream(DUMP_FILE, FileMode.Create);
                string eol = data.EndsWith("\n") ? "" : "\n";
                data = $"{DateTime.Now} | {data}{eol}";
                byte[] bytes = Encoding.UTF8.GetBytes(data);
                log_stream.Write(bytes, 0, bytes.Length);
                log_stream.Flush();
            }
        }
        [System.Diagnostics.Conditional("DEBUG")]
        static void _CloseLog()
        {
            lock (lock_log_stream)
            {
                log_stream?.Close();
                log_stream = null;
            }
        }


        class MyExportContext : IModelExportContext
        {
            readonly Document doc;
            readonly Connection con;
            readonly OrderedDict<int, QMaterialDef> material_defs;
            readonly Action on_finished;
            readonly EditableRefs editable_refs;
            readonly int level_of_detail;

            QMaterialDef current_material;   /* null for default */
            int next_id = 1;
            int next_nonedit_cdef_id = -1;   /* numbers <= 0 are for non-editable Revit geometry */
            Serializer current_serializer;
            List<Serializer> all_serializers;
            int nesting_levels;
            public string got_error { get; private set; }

            public MyExportContext(Document doc,
                                   Connection con,
                                   EditableRefs editable_refs,
                                   OrderedDict<int, QMaterialDef> material_defs,
                                   int level_of_detail,
                                   Action on_finished)
            {
                this.doc = doc;
                this.con = con;
                this.editable_refs = editable_refs;
                this.material_defs = material_defs;
                this.level_of_detail = level_of_detail;
                this.on_finished = on_finished;

                current_serializer = new Serializer();
                all_serializers = new List<Serializer> { current_serializer };
                nesting_levels = 0;
            }

            void WrapExportErrors(string fn, Action callback)
            {
                try
                {
                    callback();
                }
                catch (Exception e)
                {
                    if (got_error == null)
                        got_error = $"{e.GetType().Name} in {fn}: {e}";
                }
            }

            void OpenNestingLevel()
            {
                /* the list 'all_serializers' contains all the serializers ever used by this
                 * MyExportContext instance.  The 'current_serializer' is the one at the
                 * index 'nesting_levels'. */
                nesting_levels += 1;
                if (nesting_levels == all_serializers.Count)
                    all_serializers.Add(new Serializer());
                current_serializer = all_serializers[nesting_levels];
                current_serializer.Clear();
                _WriteLog($"nesting_level++ = {nesting_levels}\n");
            }

            void CloseNestingLevel()
            {
                nesting_levels -= 1;
                current_serializer = all_serializers[nesting_levels];
                _WriteLog($"nesting_level-- = {nesting_levels}\n");
            }

            bool IExportContext.Start()
            {
                WrapExportErrors("Start", () =>
                {
                    _WriteLog("\n========================== Start ======================\n\n");
                    con.Send(new QStartUpdate());
                });
                return true;
            }

            void IExportContext.Finish()
            {
                WrapExportErrors("Finish", () =>
                {
                    _WriteLog("\n========================== Finish ======================\n");

                    /* flush the serializer */
                    SendCurrentTopLevelSerializer(done: true);
                    con.Send(new QSetConfig());
                    EmitTextures();
                    con.Send(new QTexturesComplete());
                    on_finished();
                    //_CloseLog();
                });
                if (got_error != null)
                    ShowErrorDlg("An internal error occurred during the Revit model export", got_error);
            }

            void SendCurrentTopLevelSerializer(bool done)
            {
                /* this implements a hack to push the top-level serializer (cdef_id 0)
                 * out on the wire in incremental pieces.  If we don't do that, then
                 * VR Sketch cannot start to show anything at all before all the data
                 * is received.
                 */
                Serializer.Assert(nesting_levels == 0, "SendCurrentTopLevelSerializer but nesting_levels != 0");

                bool add_mode = current_serializer.AddMode;
                current_serializer.SendCurrentSetEntities(con, 0, name: null);

                if (!add_mode)
                    con.Send(new QModelComplete(texture_count:
                        material_defs.Values.Count(matdef => matdef.texture_filename != null)));
                else
                    con.Send(new QUpdateComplete());

                if (!done)
                {
                    current_serializer.SetAddMode();
                    con.Send(new QStartUpdate());
                }
                else
                {
                    current_serializer = null;
                    all_serializers = null;
                }
            }

            RenderNodeAction IModelExportContext.OnPoint(PointNode node)
            {
                return RenderNodeAction.Skip;
            }

            RenderNodeAction IExportContextBase.OnCurve(CurveNode node)
            {
                return RenderNodeAction.Skip;
            }

            RenderNodeAction IExportContextBase.OnPolyline(PolylineNode node)
            {
                return RenderNodeAction.Skip;
            }

            void IExportContextBase.OnLineSegment(LineSegment segment)
            {
            }

            void IExportContextBase.OnPolylineSegments(PolylineSegments segments)
            {
            }

            void IExportContextBase.OnText(TextNode node)
            {
            }

            bool IExportContext.IsCanceled()
            {
                return con.ConnectionWasClosed();
            }

            RenderNodeAction IExportContext.OnViewBegin(ViewNode node)
            {
                WrapExportErrors("OnViewBegin", () =>
                {
                    _WriteLog($"\n========================== OnViewBegin {node.ViewId} ======================\n\n");
                    node.LevelOfDetail = level_of_detail;
                });
                return RenderNodeAction.Proceed;
            }

            void IExportContext.OnViewEnd(ElementId elementId)
            {
                _WriteLog("\n========================== OnViewEnd ======================\n\n");
            }

#if false
            Serializer ds_serializer;

            void EmitDirectShape(DirectShape ds)
            {
                ....;
                var top_geom = ds.get_Geometry(new Options
                {
                    IncludeNonVisibleObjects = false,
                    DetailLevel = ViewDetailLevel.Fine,
                });
                if (top_geom == null)
                    return;    /* skipped */

                if (ds_serializer == null)
                    ds_serializer = new Serializer();

                int top_cdef_id = xxxx;
                current_serializer.SerializeGroupInstance(next_id++, top_cdef_id,
                    matdef: GetMaterial(top_geom.MaterialElement));

                var pending = new Stack<(int cdef_id, GeometryElement, string name)>();
                pending.Push((top_cdef_id, top_geom, ds.Name));

                while (pending.Count > 0)
                {
                    ds_serializer.Clear();
                    var (cdef_id, geom, cdef_name) = pending.Pop();

                    void EmitFace(Face f)
                    {
                        /* Emit a face, which must be planar, with the outer loop going around
                         * in CCW order w.r.t. the plane normal, and the other loops if any
                         * in CW order (the holes). */
                        int[] main_loop = null;
                        List<int[]> holes = null;
                        int num_main_loops = 0;
                        XYZ normal = null;
                        foreach (CurveLoop a in f.EdgeLoops)
                        {
                            int edge_flags;
                            if (a.IsOpen() || !a.HasPlane())
                            {
                                /* unexpected non-closed or non-planar loop: ignored as a face,
                                 * but we still serialize the border as standalone edges */
                                edge_flags = 0;
                            }
                            else
                            {
                                if (normal == null)
                                    normal = a.GetPlane().Normal.Normalize();
                                edge_flags = Entity.FL_NOT_STANDALONE;
                            }

                            var faceEdges = new List<int>();
                            foreach (Curve c in a)
                            {
                                IList<XYZ> t = c.Tessellate();
                                XYZ pPrev = null;
                                foreach (var point in t)
                                {
                                    if (pPrev != null)
                                    {
                                        int id = next_id++;
                                        ds_serializer.SerializeEdge(id, pPrev, point, edge_flags);
                                        faceEdges.Add(id);
                                    }
                                    pPrev = point;
                                }
                            }
                            if ((edge_flags & Entity.FL_NOT_STANDALONE) == 0)
                                continue;

                            var edges = faceEdges.ToArray();
                            if (a.IsCounterclockwise(normal))
                            {
                                main_loop = edges;
                                num_main_loops += 1;
                            }
                            else
                            {
                                if (holes == null)
                                    holes = new List<int[]>();
                                holes.Add(edges);
                            }
                        }
                        if (num_main_loops != 1)
                            return;   /* invalid face */
                        if (!f.IsTwoSided)
                            return;   /* degenerate face */

                        QMaterialDef matdef = GetMaterial(f.MaterialElementId);
                        ds_serializer.SerializeFace(next_id++, matdef, normal, main_loop, holes);
                    }

                    foreach (GeometryObject gobj in geom)
                    {
                        if (gobj == null)
                            continue;
                        if (gobj.Visibility != Visibility.Visible)
                            continue;

                        switch (gobj)
                        {
                            case GeometryElement gsubelem:
                                int subcdef_id = xxxx;
                                ds_serializer.SerializeGroupInstance(next_id++, subcdef_id,
                                    matdef: GetMaterial(gsubelem.MaterialElement));
                                pending.Push((subcdef_id, gsubelem, null));
                                break;

                            case Solid gsolid:
                                /* 'gsolid' can also be an open shell.  We're using it as
                                 * a collection of faces. */
                                foreach (Face f in gsolid.Faces)
                                    EmitFace(f);
                                break;

                            //case Face gface:   /* not supported directly inside DirectShape */
                            //    EmitFace(gface);
                            //    break;

                            case Mesh gmesh:
                                /* Meshes are present only as fallbacks if the
                                   TessellatedShapeBuilder class fails to build an open-shell
                                   Solid.  I don't know how that can be the case, but better
                                   safe than sorry. */
                                QMaterialDef matdef = GetMaterial(gmesh.MaterialElementId);
                                ds_serializer.SerializeMesh(next_id++, matdef, gmesh);
                                break;

                            case Curve gcurve:
                                XYZ prev = null;
                                foreach (var point in gcurve.Tessellate())
                                {
                                    if (prev != null)
                                    {
                                        ds_serializer.SerializeEdge(next_id, prev, point, 0);
                                        next_id++;
                                    }
                                    prev = point;
                                }
                                break;
                        }
                    }

                    ds_serializer.SendCurrentSetEntities(con, cdef_id, cdef_name);
                }

                editable_refs.Register(ds, top_cdef_id);
            }
#endif

            RenderNodeAction IExportContext.OnElementBegin(ElementId elementId)
            {
#if false
                if (doc.GetElement(elementId) is DirectShape ds && ds.ApplicationId == APPLICATION_ID)
                {
                    _WriteLog($"\n=========== DirectShape {ds.Name} ==========\n\n");
                    EmitDirectShape(ds);
                    return RenderNodeAction.Skip;
                }
#endif
                _WriteLog($"\n----------- OnElementBegin {elementId} ----------\n\n");
                return RenderNodeAction.Proceed;
            }

            void IExportContext.OnElementEnd(ElementId elementId)
            {
                _WriteLog("\n----------- OnElementEnd ----------\n\n");
            }

            RenderNodeAction IExportContext.OnInstanceBegin(InstanceNode node)
            {
                WrapExportErrors("OnInstanceBegin", () =>
                {

                    _WriteLog("\n----------- OnInstanceBegin ----------\n\n");
                    OpenNestingLevel();
                });
                return RenderNodeAction.Proceed;
            }

            void EmitGroupEnd(string name, Transform tr)
            {
                int count = current_serializer.WordCount;
                if (count > 0)
                    current_serializer.SendCurrentSetEntities(con, next_nonedit_cdef_id, name);
                CloseNestingLevel();

                /* now current_serializer is again from the parent of the instance */
                if (count > 0)
                {
                    current_serializer.SerializeGroupInstance(next_id, next_nonedit_cdef_id, tr);
                    next_id += 1;
                    next_nonedit_cdef_id -= 1;

                    if (nesting_levels == 0 && count > 12000)
                        SendCurrentTopLevelSerializer(done: false);
                }
            }

            void IExportContext.OnInstanceEnd(InstanceNode node)
            {
                WrapExportErrors("OnInstanceEnd", () =>
                {
                    _WriteLog("\n----------- OnInstanceEnd ----------\n\n");
                    EmitGroupEnd(node.NodeName, node.GetTransform());
                });
            }

            RenderNodeAction IExportContext.OnLinkBegin(LinkNode node)
            {
                WrapExportErrors("OnLinkBegin", () =>
                {
                    _WriteLog("\n----------- OnLinkBegin ----------\n\n");
                    OpenNestingLevel();
                });
                return RenderNodeAction.Proceed;
            }

            void IExportContext.OnLinkEnd(LinkNode node)
            {
                WrapExportErrors("OnLinkEnd", () =>
                {
                    _WriteLog("\n----------- OnLinkEnd ----------\n\n");
                    EmitGroupEnd(node.NodeName, node.GetTransform());
                });
            }

            RenderNodeAction IExportContext.OnFaceBegin(FaceNode node)
            {
                return RenderNodeAction.Skip;
            }

            void IExportContext.OnFaceEnd(FaceNode node)
            {
            }

            void IExportContext.OnRPC(RPCNode node)
            {
            }

            void IExportContext.OnLight(LightNode node)
            {
            }

            QMaterialDef GetMaterial(Material material)
            {
                return material != null && material.IsValidObject ? GetMaterial(material.Id) : null;
            }

            QMaterialDef GetMaterial(ElementId material_id)
            {
                int mat_id = material_id.IntegerValue;
                material_defs.TryGetValue(mat_id, out var result);
                return result;
            }

            void IExportContext.OnMaterial(MaterialNode node)
            {
                WrapExportErrors("OnMaterial", () =>
                {
                    current_material = GetMaterial(node.MaterialId);
                });
            }

            void IExportContext.OnPolymesh(PolymeshTopology node)
            {
                WrapExportErrors("OnPolymesh", () =>
                {
                    current_serializer.SerializePolymesh(next_id, current_material, node);
                    next_id += 1;
                });
            }

            void EmitTextures()
            {
                /* send all albedo textures */
                foreach (var kv in material_defs)
                {
                    int mat_id = kv.Key;
                    QMaterialDef matdef = kv.Value;
                    if (matdef.texture_filename == null)
                        continue;
                    byte[] data;
                    try
                    {
                        data = File.ReadAllBytes(matdef.texture_filename);
                    }
                    catch
                    {
                        data = Array.Empty<byte>();
                    }
                    if (data.Length == 0)
                        continue;

                    var qtex = new QTexture(GetMatName(mat_id), matdef.color_transform);
                    con.Send(qtex, data, data.Length);
                }
            }
        }

        Vector3d[] EstimateBoundingBox()
        {
            Vector3d[] bbox = null;
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent();
            
            foreach (Element e in collector)
            {
                if (!(e is Wall))    /* bah? keep only walls */
                    continue;

                //if (e.Category == null || e.Category.CategoryType != CategoryType.Model)
                //    continue;
                //
                //if (e is SpatialElement)
                //    /* these classes appear to contain "geometry" that is the bounding shape
                //     * of stuff, and not something to render (includes Room and Space) */
                //    continue;

                var g = e.get_Geometry(new Options());
                if (g == null)
                    continue;
                var bb = g.GetBoundingBox();
                if (bb == null)
                    continue;
                var min = (Vector3d)bb.Min;
                var max = (Vector3d)bb.Max;

                ///* drop the bounding box of this element if it's wildly out of bounds,
                // * which means for now outside 100 kilometers away from the origin */
                //const double MAX = 100000 / 0.3048;
                //if (min.x < -MAX || min.y < -MAX || min.z < -MAX ||
                //    max.x > MAX || max.y > MAX || max.z > MAX)
                //    continue;

                if (bbox == null)
                {
                    bbox = new Vector3d[] { min, max };
                }
                else
                {
                    bbox[0] = Vector3d.Min(bbox[0], min);
                    bbox[1] = Vector3d.Max(bbox[1], max);
                }
            }
            return bbox;
        }

        public static void WrapErrors(Action callback, string error_msg)
        {
            try
            {
                callback();
            }
            catch (Exception e)
            {
                string message = $"{e.GetType().Name}: {e}";
                ShowErrorDlg(error_msg, message);
            }
        }

        static void ShowErrorDlg(string main_content, string expanded_content)
        {
            Connection.SendError(new QTextOnController(
                "Revit\nerror", "Revit error", main_content));

            var dlg = new TaskDialog("VR Sketch - error")
            {
                MainContent = main_content,
                ExpandedContent = expanded_content,
            };
            dlg.Show();
        }

        class VRSketchEventHandler : IExternalEventHandler
        {
            public Action callback;

            void IExternalEventHandler.Execute(UIApplication app) => callback();
            string IExternalEventHandler.GetName() => nameof(VRSketchEventHandler);
        }

        public static ExternalEvent MakeSignalFromAnyThread(Action act)
        {
            return ExternalEvent.Create(new VRSketchEventHandler { callback = act });
        }

        public void SendToVR(UIApplication application, string quest_id = null)
        {
            Connection.CloseCurrentConnection();
            doc = application.ActiveUIDocument.Document;

            var view3d = doc.ActiveView as View3D;
            if (view3d == null)
            {
                view3d = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .Where(view => view.Name == "{3D}")
                    .FirstOrDefault();
                if (view3d == null)
                {
                    MessageBox.Show("VR Sketch shows the model following some settings of the " +
                              "current 3D view. " +
                              "If there is none, it uses the view called '{3D}'. " +
                              "But this view was not found either: please click on " +
                              "the 3D button in the View tab to create that view.",
                              "VR Sketch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            var con = Connection.CreateNewConnection();
            VRSketchConnect.OpenConnection(con, quest_id, () =>
            {
                /* check if objects are still valid */
                if (!con.IsCurrentConnection || !doc.IsValidObject || !view3d.IsValidObject)
                {
                    _WriteLog("OpenConnection is not starting because the state changed!\n");
                    con.Close();
                    return;
                }

                /* send headers & materials */
                con.Send(new QClearModel(EstimateBoundingBox()));

                var mats = GetElements<Material>().ToList();

                var mat_names = new string[mats.Count];
                var material_defs = new OrderedDict<int, QMaterialDef>();
                var texture_locator = new TextureLocator(doc);
                foreach (var (i, mat) in MyListExtension.Enumerate(mats))
                {
                    var mat_id = mat.Id.IntegerValue;
                    mat_names[i] = GetMatName(mat_id);

                    var c = mat.Color;
                    var cint = c.IsValid ? ((c.Blue << 16) | (c.Green << 8) | c.Red) : 0xffffff;
                    var alpha = 1f - mat.Transparency / 100f;
                    var matdef = new QMaterialDef(mat.Name, cint, alpha);
                    matdef.material_index = i;
                    texture_locator.TryGetTextureForMaterial(mat, matdef);
                    material_defs[mat_id] = matdef;
                }
                con.Send(new QMaterials(mat_names));
                foreach (var kv in material_defs)
                    con.Send(new QMaterial(GetMatName(kv.Key), kv.Value));

                var editable_refs = new EditableRefs();

                void FinishExport()
                {
                    con.interaction = new Interaction(doc, con, editable_refs, mats);
#if false
                    // can't Close() now, because it seems that the most recent Send's
                    // may not be finished yet?  No idea why!  We could try again to
                    // remove the Wait() in the Send() methods and accept that any
                    // call to Send() might actually send stuff at any later time,
                    // but attempts at doing so break the sending of the model again
                    // for no reason I understand, so meh.
                    con.Close();
#endif
                    _WriteLog("FinishExport done\n");
                }

                int level_of_detail = VRSketchApp.GetLevelOfDetail();

                var exporter = new CustomExporter(doc, new MyExportContext(
                    doc, con, editable_refs, material_defs, level_of_detail, FinishExport));
                /* for now, we don't need OnFaceBegin/OnFaceEnd.  Note that this
                 * just excludes the calls, not the actual processing of face
                 * tessellation. Meshes of the faces will still be received by the context.
                 * NOTE: setting this to true means a lot of stuff is missing, right now.
                 */
                exporter.IncludeGeometricObjects = false;
                exporter.ShouldStopOnError = false;    // try hard to get *something*
                exporter.Export(view3d);
                /* unsure from the documentation if Export() does all the exporting now or
                 * schedules it for later.  Assumes either, with a custom local function
                 * FinishExport() called from OnFinish().
                 */
            });
        }
    }
}
