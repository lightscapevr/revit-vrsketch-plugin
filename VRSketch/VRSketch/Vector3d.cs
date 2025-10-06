using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRSketch
{
    public struct Vector3d
    {
        /* This is a subset of Vector3, but with full 'double' precision */
        public double x, y, z;

        public static readonly Vector3d back = new Vector3d(0, 0, -1);
        public static readonly Vector3d down = new Vector3d(0, -1, 0);
        public static readonly Vector3d forward = new Vector3d(0, 0, 1);
        public static readonly Vector3d left = new Vector3d(-1, 0, 0);
        public static readonly Vector3d right = new Vector3d(1, 0, 0);
        public static readonly Vector3d up = new Vector3d(0, 1, 0);
        public static readonly Vector3d one = new Vector3d(1, 1, 1);
        public static readonly Vector3d zero = new Vector3d(0, 0, 0);

        public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3d operator +(Vector3d v, Vector3d w) { return new Vector3d(v.x + w.x, v.y + w.y, v.z + w.z); }
        public static Vector3d operator -(Vector3d v, Vector3d w) { return new Vector3d(v.x - w.x, v.y - w.y, v.z - w.z); }
        public static Vector3d operator -(Vector3d v) { return new Vector3d(-v.x, -v.y, -v.z); }
        public static Vector3d operator *(Vector3d v, double m) { return new Vector3d(v.x * m, v.y * m, v.z * m); }
        public static Vector3d operator *(double m, Vector3d v) { return new Vector3d(v.x * m, v.y * m, v.z * m); }
        public static Vector3d operator /(Vector3d v, double d) { return new Vector3d(v.x / d, v.y / d, v.z / d); }
        public static bool operator ==(Vector3d v, Vector3d w) { return v.x == w.x && v.y == w.y && v.z == w.z; }
        public static bool operator !=(Vector3d v, Vector3d w) { return v.x != w.x || v.y != w.y || v.z != w.z; }
        public override bool Equals(object o) { return o is Vector3d && this == (Vector3d)o; }
        public override int GetHashCode() { return (x.GetHashCode() << 2) + (y.GetHashCode() << 1) + z.GetHashCode(); }

        public static explicit operator XYZ(Vector3d v) { return new XYZ(v.x, v.y, v.z); }
        public static explicit operator Vector3d(XYZ v) { return new Vector3d(v.X, v.Y, v.Z); }

        public double magnitude { get { return Math.Sqrt(x * x + y * y + z * z); } }
        public double sqrMagnitude { get { return x * x + y * y + z * z; } }
        public static double Distance(Vector3d v, Vector3d w) { return (v - w).magnitude; }
        public Vector3d normalized { get { return this / magnitude; } }

        public static double Dot(Vector3d v, Vector3d w) { return v.x * w.x + v.y * w.y + v.z * w.z; }

        public double this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                    default: throw new IndexOutOfRangeException();
                }
            }
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    default: throw new IndexOutOfRangeException();
                }
            }
        }

        public static Vector3d Cross(Vector3d v, Vector3d w)
        {
            return new Vector3d(v.y * w.z - v.z * w.y,
                                v.z * w.x - v.x * w.z,
                                v.x * w.y - v.y * w.x);
        }

        public static Vector3d Project(Vector3d vector, Vector3d onNormal)
        {
            return onNormal * (Dot(onNormal, vector) / Dot(onNormal, onNormal));
        }

        public static Vector3d ProjectOnPlane(Vector3d vector, Vector3d planeNormal)
        {
            return vector - Project(vector, planeNormal);
        }

        public static Vector3d LerpUnclamped(Vector3d a, Vector3d b, double t)
        {
            return a * (1 - t) + b * t;
        }

        /* Extra methods */
#if false
        public int ClosestAxisPlane(double minimum)
        {
            double dx = Math.Abs(x);
            double dy = Math.Abs(y);
            double dz = Math.Abs(z);

            if (dx <= minimum && dx <= dy && dx <= dz)
                return 0;
            if (dy <= minimum && dy <= dx && dy <= dz)
                return 1;
            if (dz <= minimum && dz <= dx && dz <= dy)
                return 2;
            return -1;
        }

        public static Vector3d FromAxis(int axis)
        {
            switch (axis)
            {
                case 0: return right;
                case 1: return up;
                case 2: return forward;
                default: throw new ArgumentOutOfRangeException();
            }
        }
#endif
        public double DistanceToLine(Vector3d p1, Vector3d p2)
        {
            Vector3d v = p2 - p1;
            if (v == zero)
                return Distance(this, p1);
            return ProjectOnPlane(this - p1, v).magnitude;
        }

        public double DistanceToSegment(Vector3d p1, Vector3d p2)
        {
            return Math.Sqrt(DistanceToSegment2(p1, p2));
        }

        public double DistanceToSegment2(Vector3d p1, Vector3d p2)
        {
            Vector3d segbase = p2 - p1;
            double c2 = Dot(this - p1, segbase);
            if (c2 <= 0)
                return (this - p1).sqrMagnitude;
            if (c2 >= segbase.sqrMagnitude)
                return (this - p2).sqrMagnitude;
            return ProjectOnPlane(this - p1, segbase).sqrMagnitude;
        }

        public bool IsBetweenPoints(Vector3d p1, Vector3d p2, double epsilon)
        {
            return IsBetweenPoints2(p1, p2, epsilon * epsilon);
        }

        public bool IsBetweenPoints2(Vector3d p1, Vector3d p2, double epsilon2)
        {
            /* return DistanceToSegment(p1, p2) < epsilon; -- faster version follows */
            Vector3d segbase = p2 - p1;
            Vector3d from_p1 = this - p1;
            double c2 = Dot(from_p1, segbase);
            if (c2 <= 0)
                return from_p1.sqrMagnitude < epsilon2;
            if (c2 >= segbase.sqrMagnitude)
                return (this - p2).sqrMagnitude < epsilon2;
            return ProjectOnPlane(from_p1, segbase).sqrMagnitude < epsilon2;
        }

        public override string ToString()
        {
            return string.Format("({0:F2}, {1:F2}, {2:F2})", x, y, z);
        }

        public double this[Vector3d axis]
        {
            /* 'axis' is assumed to be normalized.  Allows to use the same syntax v[i] to read or
             * write components, but with i being from an orthonormal basis instead of 0, 1, 2 */
            get
            {
                return Dot(this, axis);
            }
            set
            {
                double old_value = Dot(this, axis);
                this += (value - old_value) * axis;
            }
        }

        public static Vector3d Min(Vector3d v, Vector3d w)
        {
            return new Vector3d(
                Math.Min(v.x, w.x),
                Math.Min(v.y, w.y),
                Math.Min(v.z, w.z));
        }

        public static Vector3d Max(Vector3d v, Vector3d w)
        {
            return new Vector3d(
                Math.Max(v.x, w.x),
                Math.Max(v.y, w.y),
                Math.Max(v.z, w.z));
        }
    }

    public struct Vector2d
    {
        /* This is a subset of Vector2, but with full 'double' precision */
        public double x, y;

        public Vector2d(double x, double y) { this.x = x; this.y = y; }
        public static Vector2d operator +(Vector2d v, Vector2d w) { return new Vector2d(v.x + w.x, v.y + w.y); }
        public static Vector2d operator -(Vector2d v, Vector2d w) { return new Vector2d(v.x - w.x, v.y - w.y); }
        public static Vector2d operator -(Vector2d v) { return new Vector2d(-v.x, -v.y); }
        public static Vector2d operator *(Vector2d v, double m) { return new Vector2d(v.x * m, v.y * m); }
        public static Vector2d operator *(double m, Vector2d v) { return new Vector2d(v.x * m, v.y * m); }
        public static Vector2d operator /(Vector2d v, double d) { return new Vector2d(v.x / d, v.y / d); }

        public static explicit operator UV(Vector2d v) { return new UV(v.x, v.y); }
        public static explicit operator Vector2d(UV v) { return new Vector2d(v.U, v.V); }

        public double magnitude { get { return Math.Sqrt(x * x + y * y); } }
        public double sqrMagnitude { get { return x * x + y * y; } }
        public static double Distance(Vector2d v, Vector2d w) { return (v - w).magnitude; }
        public static double Dot(Vector2d v, Vector2d w) { return v.x * w.x + v.y * w.y; }

        public double this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    default: throw new IndexOutOfRangeException();
                }
            }
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    default: throw new IndexOutOfRangeException();
                }
            }
        }

        public override string ToString()
        {
            return string.Format("({0:F2}, {1:F2})", x, y);
        }
    }

    public struct Matrix4x4d
    {
        public double m00, m10, m20, m30;
        public double m01, m11, m21, m31;
        public double m02, m12, m22, m32;
        public double m03, m13, m23, m33;

        public static readonly Matrix4x4d identity = new Matrix4x4d
        {
            m00 = 1,
            m10 = 0,
            m20 = 0,
            m30 = 0,
            m01 = 0,
            m11 = 1,
            m21 = 0,
            m31 = 0,
            m02 = 0,
            m12 = 0,
            m22 = 1,
            m32 = 0,
            m03 = 0,
            m13 = 0,
            m23 = 0,
            m33 = 1
        };

        struct Matrix2x2d    /* only enough to compute the Inverse conveniently */
        {
            internal double a, b, c, d;

            internal double Determinant()
            {
                return a * d - b * c;
            }

            public static Matrix2x2d operator *(Matrix2x2d A, Matrix2x2d B)
            {
                return new Matrix2x2d
                {
                    a = A.a * B.a + A.b * B.c,
                    b = A.a * B.b + A.b * B.d,
                    c = A.c * B.a + A.d * B.c,
                    d = A.c * B.b + A.d * B.d,
                };
            }

            public static Matrix2x2d operator *(double f, Matrix2x2d A)
            {
                return new Matrix2x2d { a = f * A.a, b = f * A.b, c = f * A.c, d = f * A.d };
            }

            public static Matrix2x2d operator -(Matrix2x2d A, Matrix2x2d B)
            {
                return new Matrix2x2d { a = A.a - B.a, b = A.b - B.b, c = A.c - B.c, d = A.d - B.d };
            }

            internal Matrix2x2d Adj()
            {
                return new Matrix2x2d { a = d, b = -b, c = -c, d = a };
            }

            internal static Matrix2x2d AdjMultiply(Matrix2x2d A, Matrix2x2d B)
            {
                return new Matrix2x2d
                {
                    a = A.d * B.a - A.b * B.c,
                    b = A.d * B.b - A.b * B.d,
                    c = A.a * B.c - A.c * B.a,
                    d = A.a * B.d - A.c * B.b,
                };
            }
        }

        public static Matrix4x4d Inverse(Matrix4x4d mat)
        {
            /* we represent 'mat' as a 2x2 block matrix
             *                /        \
             *                |  A  B  |
             *                |  C  D  |
             *                \        /
             * each of which is a Matrix2x2d.
             */
            var A = new Matrix2x2d { a = mat.m00, b = mat.m01, c = mat.m10, d = mat.m11 };
            var B = new Matrix2x2d { a = mat.m02, b = mat.m03, c = mat.m12, d = mat.m13 };
            var C = new Matrix2x2d { a = mat.m20, b = mat.m21, c = mat.m30, d = mat.m31 };
            var D = new Matrix2x2d { a = mat.m22, b = mat.m23, c = mat.m32, d = mat.m33 };

            double detA = A.Determinant();
            double detB = B.Determinant();
            double detC = C.Determinant();
            double detD = D.Determinant();

            var adjA_B = Matrix2x2d.AdjMultiply(A, B);
            var adjD_C = Matrix2x2d.AdjMultiply(D, C);
            double trace_adjA_B_adjD_C = (
                adjA_B.a * adjD_C.a +
                adjA_B.b * adjD_C.c +
                adjA_B.c * adjD_C.b +
                adjA_B.d * adjD_C.d);

            double invdetM = 1.0 / (detA * detD + detB * detC - trace_adjA_B_adjD_C);

            var IA = invdetM * (detD * A - B * adjD_C).Adj();
            var IB = invdetM * (detB * C - D * adjA_B.Adj()).Adj();
            var IC = invdetM * (detC * B - A * adjD_C.Adj()).Adj();
            var ID = invdetM * (detA * D - C * adjA_B).Adj();

            var result = new Matrix4x4d
            {
                m00 = IA.a,
                m10 = IA.c,
                m20 = IC.a,
                m30 = IC.c,
                m01 = IA.b,
                m11 = IA.d,
                m21 = IC.b,
                m31 = IC.d,
                m02 = IB.a,
                m12 = IB.c,
                m22 = ID.a,
                m32 = ID.c,
                m03 = IB.b,
                m13 = IB.d,
                m23 = ID.b,
                m33 = ID.d
            };
            return result;
        }

        public static Matrix4x4d operator *(Matrix4x4d a, Matrix4x4d b)
        {
            return new Matrix4x4d
            {
                m00 = a.m00 * b.m00 + a.m01 * b.m10 + a.m02 * b.m20 + a.m03 * b.m30,
                m01 = a.m00 * b.m01 + a.m01 * b.m11 + a.m02 * b.m21 + a.m03 * b.m31,
                m02 = a.m00 * b.m02 + a.m01 * b.m12 + a.m02 * b.m22 + a.m03 * b.m32,
                m03 = a.m00 * b.m03 + a.m01 * b.m13 + a.m02 * b.m23 + a.m03 * b.m33,
                m10 = a.m10 * b.m00 + a.m11 * b.m10 + a.m12 * b.m20 + a.m13 * b.m30,
                m11 = a.m10 * b.m01 + a.m11 * b.m11 + a.m12 * b.m21 + a.m13 * b.m31,
                m12 = a.m10 * b.m02 + a.m11 * b.m12 + a.m12 * b.m22 + a.m13 * b.m32,
                m13 = a.m10 * b.m03 + a.m11 * b.m13 + a.m12 * b.m23 + a.m13 * b.m33,
                m20 = a.m20 * b.m00 + a.m21 * b.m10 + a.m22 * b.m20 + a.m23 * b.m30,
                m21 = a.m20 * b.m01 + a.m21 * b.m11 + a.m22 * b.m21 + a.m23 * b.m31,
                m22 = a.m20 * b.m02 + a.m21 * b.m12 + a.m22 * b.m22 + a.m23 * b.m32,
                m23 = a.m20 * b.m03 + a.m21 * b.m13 + a.m22 * b.m23 + a.m23 * b.m33,
                m30 = a.m30 * b.m00 + a.m31 * b.m10 + a.m32 * b.m20 + a.m33 * b.m30,
                m31 = a.m30 * b.m01 + a.m31 * b.m11 + a.m32 * b.m21 + a.m33 * b.m31,
                m32 = a.m30 * b.m02 + a.m31 * b.m12 + a.m32 * b.m22 + a.m33 * b.m32,
                m33 = a.m30 * b.m03 + a.m31 * b.m13 + a.m32 * b.m23 + a.m33 * b.m33
            };
        }

        public double this[int i, int j]
        {
            get
            {
                /* IndexOutOfRangeException not detected very safely here */
                switch (i + j * 4)
                {
                    case 0: return m00;
                    case 1: return m10;
                    case 2: return m20;
                    case 3: return m30;
                    case 4: return m01;
                    case 5: return m11;
                    case 6: return m21;
                    case 7: return m31;
                    case 8: return m02;
                    case 9: return m12;
                    case 10: return m22;
                    case 11: return m32;
                    case 12: return m03;
                    case 13: return m13;
                    case 14: return m23;
                    case 15: return m33;
                    default: throw new IndexOutOfRangeException();
                }
            }
            set
            {
                /* IndexOutOfRangeException not detected very safely here */
                switch (i + j * 4)
                {
                    case 0: m00 = value; break;
                    case 1: m10 = value; break;
                    case 2: m20 = value; break;
                    case 3: m30 = value; break;
                    case 4: m01 = value; break;
                    case 5: m11 = value; break;
                    case 6: m21 = value; break;
                    case 7: m31 = value; break;
                    case 8: m02 = value; break;
                    case 9: m12 = value; break;
                    case 10: m22 = value; break;
                    case 11: m32 = value; break;
                    case 12: m03 = value; break;
                    case 13: m13 = value; break;
                    case 14: m23 = value; break;
                    case 15: m33 = value; break;
                    default: throw new IndexOutOfRangeException();
                }
            }
        }

        public static bool operator ==(Matrix4x4d a, Matrix4x4d b)
        {
            return a.m00 == b.m00 && a.m10 == b.m10 && a.m20 == b.m20 && a.m30 == b.m30 &&
                   a.m01 == b.m01 && a.m11 == b.m11 && a.m21 == b.m21 && a.m31 == b.m31 &&
                   a.m02 == b.m02 && a.m12 == b.m12 && a.m22 == b.m22 && a.m32 == b.m32 &&
                   a.m03 == b.m03 && a.m13 == b.m13 && a.m23 == b.m23 && a.m33 == b.m33;
        }

        public static bool operator !=(Matrix4x4d a, Matrix4x4d b)
        {
            return !(a == b);
        }

        public override bool Equals(object o)
        {
            return o is Matrix4x4d && this == (Matrix4x4d)o;
        }

        public override int GetHashCode()
        {
            int code = 0;
            code = (code * 3) + m00.GetHashCode();
            code = (code * 3) + m10.GetHashCode();
            code = (code * 3) + m20.GetHashCode();
            code = (code * 3) + m30.GetHashCode();
            code = (code * 3) + m01.GetHashCode();
            code = (code * 3) + m11.GetHashCode();
            code = (code * 3) + m21.GetHashCode();
            code = (code * 3) + m31.GetHashCode();
            code = (code * 3) + m02.GetHashCode();
            code = (code * 3) + m12.GetHashCode();
            code = (code * 3) + m22.GetHashCode();
            code = (code * 3) + m32.GetHashCode();
            code = (code * 3) + m03.GetHashCode();
            code = (code * 3) + m13.GetHashCode();
            code = (code * 3) + m23.GetHashCode();
            code = (code * 3) + m33.GetHashCode();
            return code;
        }

        /*public static explicit operator Matrix4x4(Matrix4x4d mat)
        {
            var result = new Matrix4x4();
            result.m00 = (float)mat.m00;
            result.m10 = (float)mat.m10;
            result.m20 = (float)mat.m20;
            result.m30 = (float)mat.m30;
            result.m01 = (float)mat.m01;
            result.m11 = (float)mat.m11;
            result.m21 = (float)mat.m21;
            result.m31 = (float)mat.m31;
            result.m02 = (float)mat.m02;
            result.m12 = (float)mat.m12;
            result.m22 = (float)mat.m22;
            result.m32 = (float)mat.m32;
            result.m03 = (float)mat.m03;
            result.m13 = (float)mat.m13;
            result.m23 = (float)mat.m23;
            result.m33 = (float)mat.m33;
            return result;
        }

        public static explicit operator Matrix4x4d(Matrix4x4 mat)
        {
            var result = new Matrix4x4d();
            result.m00 = (double)mat.m00;
            result.m10 = (double)mat.m10;
            result.m20 = (double)mat.m20;
            result.m30 = (double)mat.m30;
            result.m01 = (double)mat.m01;
            result.m11 = (double)mat.m11;
            result.m21 = (double)mat.m21;
            result.m31 = (double)mat.m31;
            result.m02 = (double)mat.m02;
            result.m12 = (double)mat.m12;
            result.m22 = (double)mat.m22;
            result.m32 = (double)mat.m32;
            result.m03 = (double)mat.m03;
            result.m13 = (double)mat.m13;
            result.m23 = (double)mat.m23;
            result.m33 = (double)mat.m33;
            return result;
        }*/

        public Vector3d MultiplyPoint(Vector3d point)
        {
            Vector3d result;
            double num = 1.0 / (m30 * point.x + m31 * point.y + m32 * point.z + m33);
            result.x = (m00 * point.x + m01 * point.y + m02 * point.z + m03) * num;
            result.y = (m10 * point.x + m11 * point.y + m12 * point.z + m13) * num;
            result.z = (m20 * point.x + m21 * point.y + m22 * point.z + m23) * num;
            return result;
        }

        public Vector3d MultiplyVector(Vector3d vector)
        {
            Vector3d result;
            result.x = m00 * vector.x + m01 * vector.y + m02 * vector.z;
            result.y = m10 * vector.x + m11 * vector.y + m12 * vector.z;
            result.z = m20 * vector.x + m21 * vector.y + m22 * vector.z;
            return result;
        }

        /* Extra methods */
        public double RoughScaleSqrt3()
        {
            return Math.Sqrt(
                m00 * m00 + m01 * m01 + m02 * m02 +
                m10 * m10 + m11 * m11 + m12 * m12 +
                m20 * m20 + m21 * m21 + m22 * m22);
        }

        /*Vector3 Forward()
        {
            Vector3 forward;
            forward.x = (float)m02;
            forward.y = (float)m12;
            forward.z = (float)m22;
            return forward;
        }

        Vector3 Upwards()
        {
            Vector3 upwards;
            upwards.x = (float)m01;
            upwards.y = (float)m11;
            upwards.z = (float)m21;
            return upwards;
        }

        Vector3 Sideways()
        {
            Vector3 sideways;
            sideways.x = (float)m00;
            sideways.y = (float)m10;
            sideways.z = (float)m20;
            return sideways;
        }

        public Quaternion ExtractRotation()
        {
            return Quaternion.LookRotation((Vector3)Forward(), (Vector3)Upwards());
        }

        public Vector3 ExtractPosition()
        {
            Vector3 position;
            position.x = (float)m03;
            position.y = (float)m13;
            position.z = (float)m23;
            return position;
        }

        bool NegativeDet()
        {
            return Vector3.Dot(Vector3.Cross(Forward(), Upwards()), Sideways()) > 0.0;
        }

        public Vector3 ExtractScale()
        {
            Vector3 scale;
            scale.x = new Vector4((float)m00, (float)m10, (float)m20, (float)m30).magnitude;
            scale.y = new Vector4((float)m01, (float)m11, (float)m21, (float)m31).magnitude;
            scale.z = new Vector4((float)m02, (float)m12, (float)m22, (float)m32).magnitude;
            if (NegativeDet())
                scale.x = -scale.x;
            return scale;
        }*/

        public static Matrix4x4d GetTranslationScalingMatrix(Vector3d translation, Vector3d scale)
        {
            // Result: 'matrix built from translation' * 'matrix built from scale'
            Matrix4x4d result = Matrix4x4d.identity;
            result.m03 = translation.x;
            result.m13 = translation.y;
            result.m23 = translation.z;
            result.m00 = scale.x;
            result.m11 = scale.y;
            result.m22 = scale.z;
            return result;
        }

        public static Matrix4x4d GetMatrixFrom3x3(Vector3d img100, Vector3d img010, Vector3d img001)
        {
            Matrix4x4d result = Matrix4x4d.identity;
            result.m00 = img100.x; result.m01 = img010.x; result.m02 = img001.x;
            result.m10 = img100.y; result.m11 = img010.y; result.m12 = img001.y;
            result.m20 = img100.z; result.m21 = img010.z; result.m22 = img001.z;
            return result;
        }
    }

    /*public static class Matrix4x4Extensions
    {
        public static bool NegativeDet(this Matrix4x4 mat)
        {
            Vector3 forward, upwards, sideways;
            forward.x = mat.m02;
            forward.y = mat.m12;
            forward.z = mat.m22;
            upwards.x = mat.m01;
            upwards.y = mat.m11;
            upwards.z = mat.m21;
            sideways.x = mat.m00;
            sideways.y = mat.m10;
            sideways.z = mat.m20;
            return Vector3.Dot(Vector3.Cross(forward, upwards), sideways) > 0.0;
        }
    }*/
}
