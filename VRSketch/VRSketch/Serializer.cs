using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;


namespace VRSketch
{
    public class BytesData
    {
        double[] _dbuf = new double[3];
        int[] _ibuf = new int[3];
        float[] _fbuf = new float[3];

        byte[] data = new byte[8192];
        int position;

        public void Clear()
        {
            position = 0;
        }

        /* the List-like methods emulate a list of doubles */
        public int Count => position >> 3;

        int Reserve(int add_amount)
        {
            int pos0 = position;
            position += add_amount;
            if (position > data.Length)
            {
                int new_size = position;
                new_size += new_size >> 2;
                Array.Resize(ref data, new_size);
            }
            return pos0;
        }

        public void Add(double x)
        {
            int pos0 = Reserve(8);
            _dbuf[0] = x;
            Buffer.BlockCopy(_dbuf, 0, data, pos0, 8);
        }
        public void AddRange(double[] xs)
        {
            int len8 = xs.Length << 3;
            int pos0 = Reserve(len8);
            Buffer.BlockCopy(xs, 0, data, pos0, len8);
        }
        public double this[int index]
        {
            get
            {
                return BitConverter.ToDouble(data, index << 3);
            }
            set
            {
                _dbuf[0] = value;
                Buffer.BlockCopy(_dbuf, 0, data, index << 3, 8);
            }
        }
        public void GetBytes(out byte[] bytes, out int length)
        {
            bytes = data;
            length = position;
        }

        /* operations to fill in data more compactly than 'double' */
        public void AddByte(byte b)
        {
            int pos0 = Reserve(1);
            data[pos0] = b;
        }
        public void AddInt(int x)
        {
            int pos0 = Reserve(4);
            _ibuf[0] = x;
            Buffer.BlockCopy(_ibuf, 0, data, pos0, 4);
        }
        public void AddInt2(int x, int y)
        {
            int pos0 = Reserve(8);
            _ibuf[0] = x;
            _ibuf[1] = y;
            Buffer.BlockCopy(_ibuf, 0, data, pos0, 8);
        }
        public void AddInt3(int x, int y, int z)
        {
            int pos0 = Reserve(12);
            _ibuf[0] = x;
            _ibuf[1] = y;
            _ibuf[2] = z;
            Buffer.BlockCopy(_ibuf, 0, data, pos0, 12);
        }
        public void AddFloat(float x)
        {
            int pos0 = Reserve(4);
            _fbuf[0] = x;
            Buffer.BlockCopy(_fbuf, 0, data, pos0, 4);
        }
        public void AddPoint2f(float x, float y)
        {
            int pos0 = Reserve(8);
            _fbuf[0] = x;
            _fbuf[1] = y;
            Buffer.BlockCopy(_fbuf, 0, data, pos0, 8);
        }
        public void AddPoint3f(float x, float y, float z)
        {
            int pos0 = Reserve(12);
            _fbuf[0] = x;
            _fbuf[1] = y;
            _fbuf[2] = z;
            Buffer.BlockCopy(_fbuf, 0, data, pos0, 12);
        }
        public void AddPoint3d(double x, double y, double z)
        {
            int pos0 = Reserve(24);
            _dbuf[0] = x;
            _dbuf[1] = y;
            _dbuf[2] = z;
            Buffer.BlockCopy(_dbuf, 0, data, pos0, 24);
        }
        public void Pad8()
        {
            int current = position & 7;
            if (current > 0)
            {
                int length = 8 - current;
                int pos0 = Reserve(length);
                Array.Clear(data, pos0, length);
            }
        }
    }


    public struct SymEdge : IEquatable<SymEdge>
    {
        /* This is really a set of two points, non-oriented. */
        public readonly Vector3d v1, v2;
        public SymEdge(Vector3d v1, Vector3d v2) { this.v1 = v1; this.v2 = v2; }
        public override int GetHashCode() { return v1.GetHashCode() + v2.GetHashCode(); }  /* symmetrical */
        public override bool Equals(object obj) { return obj is SymEdge edge && Equals(edge); }
        public bool Equals(SymEdge other) { return (other.v1 == v1 && other.v2 == v2) || (other.v1 == v2 && other.v2 == v1); }
        public SymEdge Reverse() { return new SymEdge(v2, v1); }
    }


    public class Serializer
    {
        readonly BytesData rawData = new BytesData();
        int _block_start;
        bool _add_mode;

        void StartBlock()
        {
            _block_start = rawData.Count;
            rawData.Add(0);
            rawData.Add(0);
        }

        void StopBlock(int type, int entity_id)
        {
            int start = _block_start;
            int num_entries = rawData.Count - start;   /* includes the two words of header */
            rawData[start] = (double)(type + (num_entries << 10));
            rawData[start + 1] = (double)entity_id;
        }

        public void SerializeGroupInstance(int id, int groupId, Transform tr = null,
                                           QMaterialDef matdef = null)
        {
            StartBlock();
            rawData.Add((double)groupId);
            int mat_index = GetMaterialIndex(matdef);
            rawData.Add(mat_index);
            // transformation
            double[] tr1;
            if (tr != null)
            {
                XYZ C0 = tr.BasisX;
                XYZ C1 = tr.BasisY;
                XYZ C2 = tr.BasisZ;
                XYZ C3 = tr.Origin;
                tr1 = new double[]
                {
                    C0.X, C0.Y, C0.Z, 0.0,
                    C1.X, C1.Y, C1.Z, 0.0,
                    C2.X, C2.Y, C2.Z, 0.0,
                    Convert(C3.X), Convert(C3.Y), Convert(C3.Z), 1.0,
                };
            }
            else
            {
                tr1 = new double[] { 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0 };
            }
            rawData.AddRange(tr1);
            StopBlock(3, id);
        }

        public static double Convert(double x)
        {
            return x * 12.0; // converts feet -> inches
        }

        public static double[] Convert(Vector3d[] points)
        {
            double[] result = new double[points.Length * 3];
            int i = 0;
            foreach (var pt in points)
            {
                result[i++] = Convert(pt.x);
                result[i++] = Convert(pt.y);
                result[i++] = Convert(pt.z);
            }
            return result;
        }

        public void SerializeEdge(int id, XYZ p0, XYZ p1, int flags)
        {
            StartBlock();
            int c = 1 | flags;
            rawData.AddRange(new double[] { Convert(p0.X), Convert(p0.Y), Convert(p0.Z),
                Convert(p1.X), Convert(p1.Y), Convert(p1.Z) });
            StopBlock(c, id);
        }

        public void SerializeFace(int id, QMaterialDef matdef, XYZ normal, int[] mainLoop, List<int[]> innerLoops = null)
        {
            StartBlock();
            int mat_index = GetMaterialIndex(matdef);
            rawData.AddRange(new double[] { normal.X, normal.Y, normal.Z, mat_index, mat_index });
            /*for (int i = 0; i < 8; i++)
                rawData.Add(0.0);*/
            for (int i = 0; i < mainLoop.Length; i++)
                rawData.Add((double)mainLoop[i]);
            if (innerLoops != null)
            {
                foreach (int[] innerLoop in innerLoops)
                {
                    rawData.Add(0.5);
                    for (int i = 0; i < innerLoop.Length; ++i)
                        rawData.Add((double)innerLoop[i]);
                }
            }
            rawData.Add(0.0);
            StopBlock(5, id);
        }

        
        int GetMaterialIndex(QMaterialDef matdef)
        {
            return matdef != null ? matdef.material_index : -1;
        }

        const byte B_POSITION = (byte)'P';
        const byte B_EDGE = (byte)'E';
        const byte B_TEXCOORDS = (byte)'T';
        const byte B_TEXCOORDS_BACK = (byte)'B';
        const byte B_NORMAL = (byte)'N';

        const uint NORMAL_PU_MASK = 0x7fff;
        const int NORMAL_ZSIGN_SHIFT = 15;
        const int NORMAL_PV_MASK_SHIFT = 17;

        static readonly double[] IDENTITY_UVMAP = new double[] { 1, 0, 0, 0, 1, 0 };

        void SerializeInternalMesh(int meshId, QMaterialDef matdef, IList<XYZ> all_vertices,
                                   IList<(int V1, int V2, int V3)> facets,
                                   IList<UV> uvs = null,
                                   IList<XYZ> normals = null)
        {
            if (all_vertices.Count == 0)
                return;

            StartBlock();

            Vector3d bmin = (Vector3d)all_vertices[0];
            Vector3d bmax = bmin;
            foreach (var v1 in all_vertices)
            {
                Vector3d v = (Vector3d)v1;
                bmin = Vector3d.Min(bmin, v);
                bmax = Vector3d.Max(bmax, v);
            }
            Vector3d mesh_bbox_center = (bmin + bmax) * 0.5;

            var edge_usages = new Dictionary<SymEdge, int>();
            void CountEdgeUsage(int v1, int v2)
            {
                var key = new SymEdge((Vector3d)all_vertices[v1], (Vector3d)all_vertices[v2]);
                edge_usages.TryGetValue(key, out int value);
                edge_usages[key] = value + 1;
            }
            bool EdgeIsHidden(int v1, int v2)
            {
                var key = new SymEdge((Vector3d)all_vertices[v1], (Vector3d)all_vertices[v2]);
                return edge_usages.TryGetValue(key, out int value) && value == 2;
            }

            var faces_tri = new List<(float dist, int face_index)>();
            for (int i = 0; i < facets.Count; i++)
            {
                var t = facets[i];

                CountEdgeUsage(t.V1, t.V2);
                CountEdgeUsage(t.V2, t.V3);
                CountEdgeUsage(t.V3, t.V1);

                /* sort the triangles using some crude metric that should give the same answer for
                 * triangles in the same plane.  The goal is that if an original face is, say, a
                 * square wall, then after it is triangulated all the triangles still end up next
                 * to each other.  Otherwise, if we have transparent materials, in some cases the
                 * triangulation becomes visible, because some of the triangles end up behind or
                 * in front of other parts of the mesh, just by being in behind or in front of
                 * them in the list.  This is the case at least in VR Sketch 20.0 and it's kind
                 * of expensive to fix in VR Sketch, so for now we use this workaround.
                 */
                Vector3d v0 = (Vector3d)all_vertices[t.V1];
                Vector3d v1 = (Vector3d)all_vertices[t.V2];
                Vector3d v2 = (Vector3d)all_vertices[t.V3];

                var normal = Vector3d.Cross(v1 - v0, v2 - v1);
                double length = normal.magnitude;
                double distance_along_normal = Vector3d.Dot(normal, mesh_bbox_center - v0);
                if (length > 1e-10)
                    distance_along_normal /= length;

                faces_tri.Add((dist: (float)distance_along_normal, face_index: i));
            }
            faces_tri.Sort((t1, t2) => t1.dist.CompareTo(t2.dist));

            if (uvs != null && uvs.Count < all_vertices.Count)
                uvs = null;

            if (normals != null && normals.Count < all_vertices.Count)
                normals = null;

            rawData.AddByte(B_POSITION);
            rawData.AddByte(3 * 8);
            rawData.AddByte(B_EDGE);
            rawData.AddByte(1);
            if (uvs != null)
            {
                rawData.AddByte(B_TEXCOORDS);
                rawData.AddByte(2 * 4);
            }
            if (normals != null)
            {
                rawData.AddByte(B_NORMAL);
                rawData.AddByte(4);
            }
            rawData.AddByte(0);
            rawData.AddInt(faces_tri.Count * 3);

            uint PackNormal(XYZ n)
            {
                const int pdiff = (int)((NORMAL_PU_MASK + 1) / 2);

                double norm1 = Math.Abs(n.X) + Math.Abs(n.Y) + Math.Abs(n.Z);
                norm1 = Math.Max(norm1, 1e-8f);
                double corr = (pdiff - 4) / norm1;

                int pu = (int)Math.Round(n.X * corr);   /* -16380 ... 16380 */
                int pv = (int)Math.Round(n.Y * corr);   /* -16380 ... 16380 */

                uint zsign = n.Z < 0f ? (1U << NORMAL_ZSIGN_SHIFT) : 0U;
                return (unchecked((uint)pv) << NORMAL_PV_MASK_SHIFT) |
                    ((uint)(pu + pdiff) & NORMAL_PU_MASK) | zsign;
            }

            double[] uvmap = matdef?.texture_uvmap ?? IDENTITY_UVMAP;

            void EmitVertex(int v, int next_v)
            {
                XYZ p = all_vertices[v];
                rawData.AddPoint3d(Convert(p.X), Convert(p.Y), Convert(p.Z));
                bool shown = !EdgeIsHidden(v, next_v);
                rawData.AddByte(shown ? (byte)1 : (byte)0);
                if (uvs != null)
                {
                    var uv = uvs[v];
                    rawData.AddPoint2f(
                        (float)(uvmap[0] * uv.U + uvmap[1] * uv.V + uvmap[2]),
                        (float)(uvmap[3] * uv.U + uvmap[4] * uv.V + uvmap[5]));
                }
                if (normals != null)
                {
                    var normal = normals[v];
                    uint packed = PackNormal(normal);
                    rawData.AddInt(unchecked((int)packed));
                }
            }

            foreach (var (dist, face_index) in faces_tri)
            {
                var t = facets[face_index];
                int A = t.V1;
                int B = t.V2;
                int C = t.V3;
                /* emit triangle A, B, C */
                EmitVertex(A, B);
                EmitVertex(B, C);
                EmitVertex(C, A);
            }

            int mat_index = GetMaterialIndex(matdef);
            rawData.AddInt(mat_index);
            rawData.AddInt(mat_index);
            rawData.Pad8();
            StopBlock(7, meshId);
        }

        public void SerializePolymesh(int meshId, QMaterialDef matdef, PolymeshTopology mesh)
        {
            if (!mesh.IsValidObject)
                return;

            IList<XYZ> all_vertices = mesh.GetPoints();
            IList<PolymeshFacet> all_facets = mesh.GetFacets();
            var facets = new (int, int, int)[all_facets.Count];
            for (int i = 0; i < facets.Length; i++)
            {
                var t = all_facets[i];
                facets[i] = (t.V1, t.V2, t.V3);
            }
            SerializeInternalMesh(meshId, matdef, all_vertices, facets,
                mesh.GetUVs(), mesh.GetNormals());
        }

        public void SerializeMesh(int meshId, QMaterialDef matdef, Mesh mesh)
        {
            IList<XYZ> all_vertices = mesh.Vertices;
            var facets = new (int, int, int)[mesh.NumTriangles];
            for (int i = 0; i < facets.Length; i++)
            {
                var t = mesh.get_Triangle(i);
                facets[i] = ((int)t.get_Index(0),
                             (int)t.get_Index(1),
                             (int)t.get_Index(2));
            }
            SerializeInternalMesh(meshId, matdef, all_vertices, facets);
            /* XXX UVs and Normals are ignored for now in this case */
        }

        public void Clear()
        {
            rawData.Clear();
        }

        public void SendCurrentSetEntities(Connection con, int cdef_id, string name)
        {
            QCommandWithLength cmd;
            if (_add_mode)
                cmd = new QAddEntities(cdef_id);
            else
                cmd = new QSetEntities(cdef_id, name);
            con.Send(cmd, rawData);
            rawData.Clear();
            _add_mode = false;
        }

        public void SetAddMode()
        {
            Assert(WordCount == 0, "SetAddMode but WordCount != 0");
            rawData.Add(0);   /* number of entities modified/removed */
            _add_mode = true;
        }

        public int WordCount => rawData.Count;
        public bool AddMode => _add_mode;

        public static void Assert(bool cond, string msg)
        {
            if (!cond)
            {
                VRSketchCommand._WriteLog($"ASSERT FAILED: {msg}\n");
                throw new Exception(msg);
            }
        }
    }
}
