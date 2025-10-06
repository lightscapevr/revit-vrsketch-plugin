using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;


namespace VRSketch
{
    class TextureLocator
    {
        const string MAIN_ROOT = @"C:\Program Files (x86)\Common Files\Autodesk Shared\Materials\Textures";

        Document doc;
        Dictionary<string, Asset> preset_appearance_assets;

        public TextureLocator(Document doc)
        {
            this.doc = doc;
        }

        static bool Invalid(Element x) => x == null || !x.IsValidObject;
        static bool Invalid(AssetProperty x) => x == null || !x.IsValidObject;
        static bool Invalid(Color x) => x == null || !x.IsValid;

        public bool TryGetTextureForMaterial(Material mat, QMaterialDef matdef)
        {
            if (mat.AppearanceAssetId == ElementId.InvalidElementId)
                return false;

            using (var appearance = doc.GetElement(mat.AppearanceAssetId) as AppearanceAssetElement)
            {
                if (Invalid(appearance))
                    return false;

                using (Asset asset_from_document = appearance.GetRenderingAsset())
                {
                    // The retrieved Asset may be empty if it is loaded from material library without
                    // any modification. In this case, you can use Application.GetAssets(AssetType.Appearance)
                    // to load all preset appearance assets, and retrieve the asset by its name.
                    Asset asset = asset_from_document;
                    if (Invalid(asset))
                    {
                        if (preset_appearance_assets == null)
                        {
                            preset_appearance_assets = new Dictionary<string, Asset>();
                            foreach (var asset1 in doc.Application.GetAssets(AssetType.Appearance))
                                preset_appearance_assets[asset1.Name] = asset1;
                        }
                        asset = preset_appearance_assets[appearance.Name];
                        if (Invalid(asset))
                            return false;
                    }

                    AssetProperty diffuse_prop = asset.FindByName("generic_diffuse");
                    if (Invalid(diffuse_prop))
                        return false;
                    var diffuse = diffuse_prop.GetSingleConnectedAsset();
                    if (Invalid(diffuse))
                        return false;

                    var bitmap_prop = diffuse.FindByName("unifiedbitmap_Bitmap") as AssetPropertyString;
                    if (Invalid(bitmap_prop))
                        return false;

                    string path = bitmap_prop.Value;   // e.g. @"1\mats\sitework.planting.grass.thick.png"
                    if (string.IsNullOrEmpty(path))
                        return false;
                    
                    bool rooted;
                    try
                    {
                        rooted = System.IO.Path.IsPathRooted(path);
                    }
                    catch (ArgumentException)   // illegal characters in path
                    {
                        return false;
                    }
                    if (!rooted)
                    {
                        if (path.StartsWith(@"1\"))   /* XXX really do that? */
                        {
                            string other_path = @"3\" + path.Substring(2);
                            if (System.IO.File.Exists(System.IO.Path.Combine(MAIN_ROOT, other_path)))
                                path = other_path;
                        }
                        path = System.IO.Path.Combine(MAIN_ROOT, path);
                    }
                    if (System.IO.File.Exists(path))
                    {
                        double GetDouble(Asset obj, string name, double default_value)
                        {
                            var prop = obj.FindByName(name);
                            if (!Invalid(prop))
                            {
                                if (prop is AssetPropertyDouble pdbl)
                                {
                                    VRSketchCommand._WriteLog($"'{name}' => double {pdbl.Value}\n");
                                    return pdbl.Value;
                                }
                                if (prop is AssetPropertyDistance pdist)
                                {
                                    /* xxx should use pdist's own unit specification,
                                     * with methods from Autodesk.Revit.DB.UnitUtils */
                                    double result = pdist.Value; //Serializer.Convert(pdist.Value);
                                    VRSketchCommand._WriteLog($"'{name}' => distance {result}\n");
                                    return result;
                                }
                            }
                            VRSketchCommand._WriteLog($"missing property '{name}'\n");
                            return default_value;
                        }

                        /* here go diffuse.FindByName("texture_UOffset") as AssetPropertyDouble,
                         * "texture_VOffset", "texture_UScale", "texture_VScale", "texture_WAngle"
                         */
                        double texture_RealWorldOffsetX = GetDouble(diffuse, "texture_RealWorldOffsetX", 0);
                        double texture_RealWorldOffsetY = GetDouble(diffuse, "texture_RealWorldOffsetY", 0);
                        double texture_RealWorldScaleX = GetDouble(diffuse, "texture_RealWorldScaleX", 1);
                        double texture_RealWorldScaleY = GetDouble(diffuse, "texture_RealWorldScaleY", 1);
                        double texture_UOffset = GetDouble(diffuse, "texture_UOffset", 0);
                        double texture_VOffset = GetDouble(diffuse, "texture_VOffset", 0);
                        double texture_UScale = GetDouble(diffuse, "texture_UScale", 1);
                        double texture_VScale = GetDouble(diffuse, "texture_VScale", 1);
                        double texture_WAngle = GetDouble(diffuse, "texture_WAngle", 0);

                        /* XXX double-check the formulas with examples.  Current guess is it is:
                         * input UV => apply texture_U/VScale
                         *          => apply texture_U/VOffset
                         *          => apply texture_WAngle
                         *          => apply texture_RealWorldScaleX/Y
                         *          => apply texture_RealWorldOffsetX/Y
                         */
                        Matrix4x4d m_scale_offset = Matrix4x4d.GetTranslationScalingMatrix(
                            new Vector3d(texture_UOffset,
                                         texture_VOffset,
                                         0),
                            new Vector3d(texture_UScale,
                                         texture_VScale,
                                         1));
                        double angle = texture_WAngle * Math.PI / 180.0;
                        Matrix4x4d m_angle = Matrix4x4d.GetMatrixFrom3x3(
                            new Vector3d(Math.Cos(angle), Math.Sin(angle), 1),
                            new Vector3d(-Math.Sin(angle), Math.Cos(angle), 1),
                            new Vector3d(0, 0, 1));
                        Matrix4x4d m_realworld = Matrix4x4d.GetTranslationScalingMatrix(
                            new Vector3d(texture_RealWorldOffsetX,
                                         texture_RealWorldOffsetY,
                                         0),
                            new Vector3d(texture_RealWorldScaleX,
                                         texture_RealWorldScaleY,
                                         1));
                        Matrix4x4d m = Matrix4x4d.Inverse(m_realworld) * m_angle * m_scale_offset;

                        var uvmap = new double[]
                        {
                            m.m00, m.m01, m.m03,
                            m.m10, m.m11, m.m13,
                        };
                        VRSketchCommand._WriteLog($"uvmap for '{mat.Name}':\n" +
                            $"\t{uvmap[0]}\t{uvmap[1]}\t{uvmap[2]}\n" +
                            $"\t{uvmap[3]}\t{uvmap[4]}\t{uvmap[5]}\n");

                        /* this blends black (0) and the texture color (1) */
                        float rgb_amount = (float)GetDouble(diffuse, "unifiedbitmap_RGBAmount", 1);
                        /* this blends the single solid color (0) and the previous result (1). */
                        float image_fade = (float)GetDouble(asset, "generic_diffuse_image_fade", 1);
                        /* the solid color is actually the one found here (and not in mat.Color or
                         * common_Tint_color or elsewhere).  Some Revit API docs say it is ignored
                         * if there is a texture, but actually, it is only ignored if there is a
                         * texture AND 'image_fade' has its default value of 1 (in this case we
                         * will multiply 'add_color' by 0) */
                        var c = (diffuse_prop as AssetPropertyDoubleArray4d).GetValueAsColor();

                        Vector3d add_color;
                        if (!Invalid(c))
                        {
                            add_color = new Vector3d(c.Red, c.Green, c.Blue) / 255.0;
                            VRSketchCommand._WriteLog($"add_color for '{mat.Name}': {add_color}\n");
                            add_color *= 1 - image_fade;
                        }
                        else
                        {
                            add_color = Vector3d.zero;
                            image_fade = 1;
                        }
                        float texf = rgb_amount * image_fade;

                        var color_transform = rgb_amount == 1 && image_fade == 1 ? null : new float[]
                        {
                            texf, 0, 0, (float)add_color.x,
                            0, texf, 0, (float)add_color.y,
                            0, 0, texf, (float)add_color.z,
                        };
                        if (color_transform != null)
                            VRSketchCommand._WriteLog($"color_transform for '{mat.Name}':\n" +
                                $"\t{color_transform[0]}\t{color_transform[1]}\t{color_transform[2]}\t{color_transform[3]}\n" +
                                $"\t{color_transform[4]}\t{color_transform[5]}\t{color_transform[6]}\t{color_transform[7]}\n" +
                                $"\t{color_transform[8]}\t{color_transform[9]}\t{color_transform[10]}\t{color_transform[11]}\n");

                        matdef.width = 1 / 0.0254;
                        matdef.height = 1 / 0.0254;
                        matdef.texture_filename = path;
                        matdef.texture_uvmap = uvmap;
                        matdef.color_transform = color_transform;
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
