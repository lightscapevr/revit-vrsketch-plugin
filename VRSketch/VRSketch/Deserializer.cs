#if false
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;


namespace VRSketch
{
    public static class Deserializer
    {
        public const double SCALE = 1.0 / 12.0;   /* converts inches -> feet */

        public static Vector3d Vec3d(double[] v1)
        {
            return new Vector3d(v1[0], v1[1], v1[2]) * SCALE;
        }

        public static Vector3d Vec3d_NoScale(double[] v1)
        {
            return new Vector3d(v1[0], v1[2], v1[1]);
        }
    }

    public class Entity
    {
        public const int T_EDGE = 1;
        public const int T_FACE = 5;
        public const int T_GROUP = 3;
        public const int T_IMAGE = 4;
        public const int T_TEXT = 6;
        public const int T_MESH = 7;   /* Rhino */
        public const int T_POLYLINE = 8;   /* Rhino */

        public const int FL_EDGE_HIDDEN = 32;
        public const int FL_NOT_STANDALONE = 64;
        public const int FL_EDGE_SOFT = 128;
        public const int FL_EDGE_SMOOTH = 256;
        public const int FL_EDGE_EXTRA = 512;
        public const int FLX_EDGE_CURVE = 1;

        public const int FL_FACE_UV1 = 32;
        public const int FL_FACE_UV2 = 64;

        public const int FL_GROUP_IS_COMPONENT = 32;
        public const int FL_GROUP_ANIMATION = 64;
        public const int FL_GROUP_LAYER = 128;     /* also used for T_IMAGE */
        public const int FL_GROUP_LOCKED = 256;
        public const int FL_GROUP_MOREFLAGS = 512;     /* also used for T_IMAGE */

        public const int FL_GROUPMORE_NO_CASTS_SHADOWS = 1;
        public const int FL_GROUPMORE_NO_RECEIVES_SHADOWS = 2;

        public const int FL_TEXT_DISPLAY_LEADER = 32;
        public const int FL_TEXT_DISPLAY_ARROW_END = 64;
        public const int FL_TEXT_DISPLAY_DOT = 128;
        public const int FL_HAS_SOUND = 256;

        public int flags, num_entries;
        public int id;

        int rawbase;
        double[] _buf = new double[12];

        public bool ParseNext(byte[] rawdata, ref int rawpos, out int t)
        {
            long lflags = (long)BitConverter.ToDouble(rawdata, rawpos);
            if (lflags == 0)
            {
                rawpos += 8;   /* end marker */
                t = 0;
                return false;
            }
            id = (int)BitConverter.ToDouble(rawdata, rawpos + 8);
            rawbase = rawpos + 16;
            flags = (int)(lflags & 1023);
            t = flags & 31;

            num_entries = (int)(lflags >> 10);
            if (num_entries == 0)
                throw new Exception("missing size in block");
            rawpos += 8 * num_entries;
            num_entries -= 2;    /* to keep in sync with the 'index' argument of ExtractXxx() */
            return true;
        }

        public Vector3d ExtractVector(byte[] rawdata, int index)
        {
            int pos = rawbase + 8 * index;
            _buf[0] = BitConverter.ToDouble(rawdata, pos);
            _buf[1] = BitConverter.ToDouble(rawdata, pos + 8);
            _buf[2] = BitConverter.ToDouble(rawdata, pos + 16);
            return Deserializer.Vec3d(_buf);
        }

        public Vector3d ExtractVector_NoScale(byte[] rawdata, int index)
        {
            int pos = rawbase + 8 * index;
            _buf[0] = BitConverter.ToDouble(rawdata, pos);
            _buf[1] = BitConverter.ToDouble(rawdata, pos + 8);
            _buf[2] = BitConverter.ToDouble(rawdata, pos + 16);
            return Deserializer.Vec3d_NoScale(_buf);
        }

        public double ExtractDouble(byte[] rawdata, int index)
        {
            return BitConverter.ToDouble(rawdata, rawbase + 8 * index);
        }

        public int ExtractInt(byte[] rawdata, int index)
        {
            return (int)BitConverter.ToDouble(rawdata, rawbase + 8 * index);
        }

        public int ExtractRawInt(byte[] rawdata, int byte_index)
        {
            return BitConverter.ToInt32(rawdata, rawbase + byte_index);
        }

        public uint ExtractRawUInt(byte[] rawdata, int byte_index)
        {
            return BitConverter.ToUInt32(rawdata, rawbase + byte_index);
        }

        public byte ExtractRawByte(byte[] rawdata, int byte_index)
        {
            return rawdata[rawbase + byte_index];
        }

        public float ExtractRawFloat(byte[] rawdata, int byte_index)
        {
            return BitConverter.ToSingle(rawdata, rawbase + byte_index);
        }

        public double ExtractRawDouble(byte[] rawdata, int byte_index)
        {
            return BitConverter.ToDouble(rawdata, rawbase + byte_index);
        }

        public (float x, float y) ExtractRawVec2(byte[] rawdata, int byte_index)
        {
            return (
                ExtractRawFloat(rawdata, byte_index),
                ExtractRawFloat(rawdata, byte_index + 4));
        }

        public (float x, float y, float z) ExtractRawVec3(byte[] rawdata, int byte_index)
        {
            return (
                ExtractRawFloat(rawdata, byte_index),
                ExtractRawFloat(rawdata, byte_index + 4),
                ExtractRawFloat(rawdata, byte_index + 8));
        }

        public Vector3d ExtractRawVec3d(byte[] rawdata, int byte_index)
        {
            return new Vector3d(
                ExtractRawDouble(rawdata, byte_index),
                ExtractRawDouble(rawdata, byte_index + 8),
                ExtractRawDouble(rawdata, byte_index + 16));
        }

        public double[] ExtractTransform(byte[] rawdata, int index)
        {
            var result = new double[16];
            for (int i = 0; i < 16; i++)
                result[i] = BitConverter.ToDouble(rawdata, rawbase + 8 * (index + i));
            return result;
        }

        public int GetFirstLoopPosition()
        {
            int result = rawbase + 8 * 5;
            if ((flags & FL_FACE_UV1) != 0) result += 8 * 12;
            if ((flags & FL_FACE_UV2) != 0) result += 8 * 12;
            return result;
        }

        public int[] FaceNextLoop(byte[] rawdata, ref int looppos, out bool done)
        {
            int count = 0;
            int rawbase = looppos;
            while (true)
            {
                double value = BitConverter.ToDouble(rawdata, rawbase + 8 * count);
                if (value == 0.0)
                {
                    done = true;
                    break;
                }
                if (value == 0.5)
                {
                    done = false;
                    break;
                }
                count++;
            }
            looppos = rawbase + 8 * (count + 1);

            int[] result = new int[count];
            for (int i = 0; i < count; i++)
                result[i] = (int)BitConverter.ToDouble(rawdata, rawbase + 8 * i);
            return result;
        }

        public int IndexFromLoopPos(int looppos) => (looppos - rawbase) / 8;

        public double[] ExtractUVs(byte[] rawdata, ref int index, int check_flag)
        {
            if ((flags & check_flag) == 0)
                return null;
            else
            {
                for (int i = 0; i < 12; i++)
                    _buf[i] = BitConverter.ToDouble(rawdata, rawbase + 8 * (index + i));
                index += 12;
                return _buf;
            }
        }
    }

    public class GroupDecoder
    {
        class GroupEdit
        {
            public List<GeometryObject> geom_objects;
            public List<> ..;
            public string name;
        }

        readonly List<Material> materials_by_index;

        struct SourceEdge
        {
            internal Vector3d pos1, pos2;
            internal bool used_in_face;
        }
        Dictionary<int, SourceEdge> source_edges;

        public GroupDecoder(List<Material> materials_by_index)
        {
            this.materials_by_index = materials_by_index;
        }

        public List<GeometryObject> DecodeGeomObjs(byte[] rawdata)
        {
            ...;



            int rawpos = 0;
            var qent = new Entity();
            source_edges = new Dictionary<int, SourceEdge>();
            while (qent.ParseNext(rawdata, ref rawpos, out int type))
            {
                if (type == Entity.T_EDGE)
                {
                    source_edges[qent.id] = new SourceEdge
                    {
                        pos1 = qent.ExtractVector(rawdata, 0),
                        pos2 = qent.ExtractVector(rawdata, 3),
                        used_in_face = (qent.flags & Entity.FL_NOT_STANDALONE) != 0,
                    };
                }
            }

            rawpos = 0;
            var geom_objects = new List<GeometryObject>();
            var builder = new TessellatedShapeBuilder
            {
                Target = TessellatedShapeBuilderTarget.AnyGeometry,
                Fallback = TessellatedShapeBuilderFallback.Mesh,
            };
            while (qent.ParseNext(rawdata, ref rawpos, out int type))
            {
                switch (type)
                {
                    case Entity.T_FACE:
                        TessellatedFace face = BuildFaceDefinition(qent, rawdata);
                        builder.OpenConnectedFaceSet(isSolid: true);
                        builder.AddFace(face);
                        builder.CloseConnectedFaceSet();
                        builder.Build();
                        var build_result = builder.GetBuildResult();
                        geom_objects.AddRange(build_result.GetGeometricalObjects());
                        builder.Clear();   /* XXX check if this is necessary */
                        break;

                    case T_GROUP:
                        ...;
                        break;
                }
            }

            foreach (var edge in source_edges.Values)
            {
                if (edge.used_in_face)
                    continue;

                Curve curve;
                try
                {
                    curve = Line.CreateBound((XYZ)edge.pos1, (XYZ)edge.pos2);
                }
                catch
                {
                    /* pos1 and pos2 might be too close to each other; ignore */
                    continue;
                }
                geom_objects.Add(curve);
            }
            return geom_objects;
        }

        IList<XYZ> ExtractLoop(int[] edges)
        {
            if (edges.Length < 2)
                throw new KeyNotFoundException("bogus loop");

            XYZ[] vertices = new XYZ[edges.Length];
            //byte[] edges_hidden = null;
            //uint[] normals = null;
            for (int i = edges.Length - 1; i >= 0; i--)
            {
                int n = edges[i];
                Vector3d pos;
                SourceEdge euse;

                if (n >= 0)
                {
                    euse = source_edges[n];   /* may raise KeyNotFoundException too */
                    pos = euse.pos1;
                }
                else
                {
                    n = -n;
                    euse = source_edges[n];   /* may raise KeyNotFoundException too */
                    pos = euse.pos2;
                }
                vertices[i] = (XYZ)pos;
                /*if (euse.hidden | euse.soft)
                {
                    if (edges_hidden == null)
                        edges_hidden = new byte[edges.Length];
                    edges_hidden[i] = FaceDefinition.Loop.EDGE_SOFT;
                }*/
                /*if (euse.smooth)
                {
                    if (edges_hidden == null)
                        edges_hidden = new byte[edges.Length];
                    edges_hidden[i] |= FaceDefinition.Loop.EDGE_SMOOTH;

                    if (normals == null)
                        normals = new uint[edges.Length];
                    int i1 = i, i2 = i;
                    if (edges[i] >= 0)
                        i2 = (i2 + 1 == normals.Length) ? 0 : i2 + 1;
                    else
                        i1 = (i1 + 1 == normals.Length) ? 0 : i1 + 1;
                    normals[i1] = FaceDefinition.Loop.PackNormal(euse.normal1);
                    normals[i2] = FaceDefinition.Loop.PackNormal(euse.normal2);
                }*/
                /*if (euse.curve)
                {
                    if (edges_hidden == null)
                        edges_hidden = new byte[edges.Length];
                    edges_hidden[i] |= FaceDefinition.Loop.EDGE_CURVE;
                }*/
                euse.used_in_face = true;
                source_edges[n] = euse;
            }
            return vertices;
        }

        ElementId GetMaterialId(int mat_index)
        {
            if (mat_index >= 0 && mat_index < materials_by_index.Count)
            {
                var mat = materials_by_index[mat_index];
                if (mat != null && mat.IsValidObject)
                    return mat.Id;
            }
            return ElementId.InvalidElementId;
        }

        TessellatedFace BuildFaceDefinition(Entity qent, byte[] rawdata)
        {
            int looppos = qent.GetFirstLoopPosition();

            List<IList<XYZ>> loops = new List<IList<XYZ>>();
            loops.Add(ExtractLoop(qent.FaceNextLoop(rawdata, ref looppos, out bool done)));
            while (!done)
                loops.Add(ExtractLoop(qent.FaceNextLoop(rawdata, ref looppos, out done)));
            
            //ExtractVector_NoScale(rawdata, 0);  /* the normal, ignored here */

            int mat_index = qent.ExtractInt(rawdata, 3);
            //ExtractInt(rawdata, 4);             /* the back-side material, ignored here */

            return new TessellatedFace(loops, GetMaterialId(mat_index));
            
            /* XXX how do we specify UVs???
            int index = 5;
            double[] uv1 = qent.ExtractUVs(rawdata, ref index, QEntity.FL_FACE_UV1);
            QCommand.UVList(uv1, out face_def.uv1, mat1);
            double[] uv2 = qent.ExtractUVs(rawdata, ref index, QEntity.FL_FACE_UV2);
            QCommand.UVList(uv2, out face_def.uv2, mat2);
            */
        }
    }
}
#endif
