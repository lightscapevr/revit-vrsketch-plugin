using System;
using System.Collections;
using System.Collections.Generic;


namespace VRSketch
{
    namespace AffineSpace
    {
        public class EmptyIntersection : Exception
        {
        }


        public abstract class AffineSubspace
        {
            /* Base class for affine subspaces of the space. */

            protected const double EPSILON = 1e-5;

            public abstract int Dimension();
            public abstract string KindName();
            public abstract Vector3d ProjectPointInside(Vector3d pt);
            public abstract AffineSubspace Intersect(AffineSubspace subspace);
            public abstract AffineSubspace UnionPoint(Vector3d pt);
            public abstract AffineSubspace Transform(Matrix4x4d transform);
            public abstract Vector3d? AdjustAtDistance(Vector3d origin, Vector3d point, double distance);
            public abstract AffineSubspace Orthogonal(Vector3d from_point);

            public virtual double AbsoluteDistanceToPoint(Vector3d pt)
            {
                /* default implementation */
                return Vector3d.Distance(pt, ProjectPointInside(pt));
            }

            public static bool TryIntersect(ref AffineSubspace subspace1, AffineSubspace subspace2)
            {
                try
                {
                    subspace1 = subspace1.Intersect(subspace2);
                }
                catch (EmptyIntersection)
                {
                    return false;
                }
                return true;
            }

            public bool Contains(AffineSubspace smaller_subspace)
            {
                int dim1 = smaller_subspace.Dimension();
                if (!TryIntersect(ref smaller_subspace, this))
                    return false;
                return dim1 == smaller_subspace.Dimension();
            }

            public bool Contains(Vector3d point)
            {
                return AbsoluteDistanceToPoint(point) < EPSILON;
            }

            public static AffineSubspace UnionOfPoints(IEnumerable<Vector3d> points)
            {
                AffineSubspace subspace = null;
                foreach (var pt in points)
                {
                    if (subspace == null)
                        subspace = new SinglePoint(pt);
                    else
                        subspace = subspace.UnionPoint(pt);
                }
                return subspace;
            }

            public AffineSubspace ProjectSubspaceInside(AffineSubspace org_space)
            {
                /* return the subspace made of the set of ProjectPointInside(pt) for pt in org_space */
                AffineSubspace result;
                result = new SinglePoint(ProjectPointInside(org_space.ProjectPointInside(Vector3d.zero)));

                for (int i = 0; i < 3; i++)
                {
                    Vector3d pt = Vector3d.zero;
                    pt[i] = 1;
                    result = result.UnionPoint(ProjectPointInside(org_space.ProjectPointInside(pt)));
                }
                return result;
            }
        }

        public class WholeSpace : AffineSubspace
        {
            public static readonly WholeSpace singleton = new WholeSpace();

            public override int Dimension()
            {
                return 3;
            }

            public override string KindName()
            {
                return "whole space";
            }

            public override Vector3d ProjectPointInside(Vector3d pt)
            {
                return pt;
            }

            public override double AbsoluteDistanceToPoint(Vector3d pt)
            {
                return 0;
            }

            public override AffineSubspace Intersect(AffineSubspace subspace)
            {
                return subspace;
            }

            public override AffineSubspace UnionPoint(Vector3d pt)
            {
                return this;
            }

            public override AffineSubspace Transform(Matrix4x4d transform)
            {
                return this;
            }

            public override Vector3d? AdjustAtDistance(Vector3d center, Vector3d point, double distance)
            {
                /* can't fail */
                Vector3d v = point - center;
                if (v.sqrMagnitude < EPSILON * EPSILON)
                    v = Vector3d.up;   /* artificial choice, but unlikely case */

                v = v.normalized * distance;
                return center + v;
            }

            public override AffineSubspace Orthogonal(Vector3d from_point)
            {
                return new SinglePoint(from_point);
            }
        }


        public class Plane : AffineSubspace
        {
            public readonly Vector3d normal;
            public readonly double distance;

            public static readonly Plane horizontal = new Plane(Vector3d.up, 0);

            public Plane(Vector3d normal, double distance)
            {
                this.normal = normal;
                this.distance = distance;
            }

            public override int Dimension()
            {
                return 2;
            }

            public override string KindName()
            {
                return "plane";
            }

            public static Plane FromPointAndNormal(Vector3d from_point, Vector3d normal)
            {
                return new Plane(normal, -Vector3d.Dot(normal, from_point));
            }

            public double SignedDistanceToPoint(Vector3d pt)
            {
                return Vector3d.Dot(normal, pt) + distance;
            }

            public override double AbsoluteDistanceToPoint(Vector3d pt)
            {
                return Math.Abs(SignedDistanceToPoint(pt));
            }

            public override Vector3d ProjectPointInside(Vector3d pt)
            {
                return pt - normal * SignedDistanceToPoint(pt);
            }

            public override AffineSubspace Intersect(AffineSubspace subspace)
            {
                if (!(subspace is Plane))
                    return subspace.Intersect(this);

                Plane plane2 = (Plane)subspace;
                Vector3d normal1 = Vector3d.Cross(normal, plane2.normal);
                double d = normal1.magnitude;
                Vector3d selfpt = normal * (-distance);
                if (d < EPSILON)
                {
                    if (plane2.AbsoluteDistanceToPoint(selfpt) < EPSILON)
                        return this;
                    else
                        throw new EmptyIntersection();
                }
                else
                {
                    normal1 /= d;
                    Vector3d in_line = Vector3d.Cross(normal, normal1);
                    double d1 = plane2.SignedDistanceToPoint(selfpt);
                    double d2 = plane2.SignedDistanceToPoint(selfpt + in_line);
                    double f = d1 / (d1 - d2);
                    Vector3d pt = selfpt + f * in_line;
                    return new Line(pt, normal1);
                }
            }

            public override AffineSubspace UnionPoint(Vector3d point)
            {
                if (AbsoluteDistanceToPoint(point) < EPSILON)
                    return this;
                return WholeSpace.singleton;
            }

            public override AffineSubspace Transform(Matrix4x4d transform)
            {
                Vector3d point = normal * (-distance);
                Vector3d inside1;
                if (Math.Abs(normal.x) >= 0.5f)
                    inside1 = new Vector3d(normal.y, -normal.x, 0);
                else
                    inside1 = new Vector3d(0, normal.z, -normal.y);
                Vector3d inside2 = Vector3d.Cross(normal, inside1);

                point = transform.MultiplyPoint(point);
                inside1 = transform.MultiplyVector(inside1);
                inside2 = transform.MultiplyVector(inside2);
                Vector3d new_normal = Vector3d.Cross(inside1, inside2);
                return Plane.FromPointAndNormal(point, new_normal.normalized);
            }

            public override Vector3d? AdjustAtDistance(Vector3d center, Vector3d point, double distance)
            {
                Vector3d proj_center = ProjectPointInside(center);
                double dist_min = Vector3d.Distance(center, proj_center);
                if (dist_min > distance + EPSILON)
                    return null;

                if (dist_min > distance - EPSILON)
                    return proj_center;
                else
                {
                    Vector3d diff = point - proj_center;
                    if (diff == Vector3d.zero)
                        return null;

                    diff = diff.normalized * Math.Sqrt(distance * distance - dist_min * dist_min);
                    return proj_center + diff;
                }
            }

            public override AffineSubspace Orthogonal(Vector3d from_point)
            {
                return new Line(from_point, normal);
            }
        }


        public class Line : AffineSubspace
        {
            public readonly Vector3d from_point, axis;   /* NOTE: 'axis' is supposed to be normalized */

            public Line(Vector3d from_point, Vector3d axis)
            {
                this.from_point = from_point;
                this.axis = axis;
            }

            public override int Dimension()
            {
                return 1;
            }

            public override string KindName()
            {
                return "line";
            }

            public override Vector3d ProjectPointInside(Vector3d pt)
            {
                var fraction = Vector3d.Dot(axis, pt - from_point);
                return from_point + axis * fraction;
            }

            public override AffineSubspace Intersect(AffineSubspace subspace)
            {
                if (subspace is SinglePoint || subspace is WholeSpace)
                    return subspace.Intersect(this);

                /* if two points on the line 'self' are also almost in 'subspace', then
                   the intersection will be the whole line 'self' */
                if (subspace.AbsoluteDistanceToPoint(from_point) < EPSILON &&
                    subspace.AbsoluteDistanceToPoint(from_point + axis) < EPSILON)
                    return this;

                /* otherwise, the intersection is at most one point */
                double d1, d2;
                if (subspace is Line)
                {
                    Line line2 = (Line)subspace;
                    Vector3d v = Vector3d.ProjectOnPlane(axis, line2.axis);
                    Vector3d vk = from_point - line2.from_point;
                    d1 = Vector3d.Dot(v, vk);
                    d2 = Vector3d.Dot(v, vk + axis);
                }
                else
                {
                    Plane plane2 = (Plane)subspace;
                    d1 = plane2.SignedDistanceToPoint(from_point);
                    d2 = plane2.SignedDistanceToPoint(from_point + axis);
                }
                if (Math.Abs(d1 - d2) < EPSILON)
                    throw new EmptyIntersection();
                double f = d1 / (d1 - d2);
                Vector3d pt = from_point + f * axis;
                if (subspace.AbsoluteDistanceToPoint(pt) > EPSILON)
                    throw new EmptyIntersection();
                return new SinglePoint(pt);
            }

            public override AffineSubspace UnionPoint(Vector3d point)
            {
                if (AbsoluteDistanceToPoint(point) < EPSILON)
                    return this;

                Vector3d normal = Vector3d.Cross(point - from_point, axis);
                return Plane.FromPointAndNormal(from_point, normal.normalized);
            }

            public override AffineSubspace Transform(Matrix4x4d transform)
            {
                Vector3d p1 = transform.MultiplyPoint(from_point);
                Vector3d a1 = transform.MultiplyVector(axis);
                return new Line(p1, a1.normalized);
            }

            public override Vector3d? AdjustAtDistance(Vector3d center, Vector3d point, double distance)
            {
                double closest_k = Vector3d.Dot(axis, center - from_point);
                Vector3d closest_on_line = from_point + axis * closest_k;

                double dist_min = Vector3d.Distance(center, closest_on_line);
                if (dist_min > distance + EPSILON)
                    return null;

                if (dist_min > distance - EPSILON)
                    return closest_on_line;
                else
                {
                    double k = Math.Sqrt(distance * distance - dist_min * dist_min);
                    if (Vector3d.Dot(axis, center) > Vector3d.Dot(axis, point))
                        k = -k;
                    return from_point + axis * (closest_k + k);
                }
            }

            public override AffineSubspace Orthogonal(Vector3d from_point)
            {
                return Plane.FromPointAndNormal(from_point, axis);
            }
        }


        public class SinglePoint : AffineSubspace
        {
            public readonly Vector3d position;

            public SinglePoint(Vector3d position)
            {
                this.position = position;
            }

            public override int Dimension()
            {
                return 0;
            }

            public override string KindName()
            {
                return "point";
            }

            public override Vector3d ProjectPointInside(Vector3d pt)
            {
                return position;
            }

            public override AffineSubspace Intersect(AffineSubspace subspace)
            {
                if (subspace.AbsoluteDistanceToPoint(position) > EPSILON)
                    throw new EmptyIntersection();
                return this;
            }

            public override AffineSubspace UnionPoint(Vector3d point)
            {
                Vector3d axis = point - position;
                if (axis.sqrMagnitude < EPSILON * EPSILON)
                    return this;
                return new Line(position, axis.normalized);
            }

            public override AffineSubspace Transform(Matrix4x4d transform)
            {
                Vector3d p1 = transform.MultiplyPoint(position);
                return new SinglePoint(p1);
            }

            public override Vector3d? AdjustAtDistance(Vector3d center, Vector3d point, double distance)
            {
                /* 'point' ignored here */
                double real_distance = Vector3d.Distance(position, center);
                if (Math.Abs(distance - real_distance) < EPSILON)
                    return position;
                else
                    return null;
            }

            public override AffineSubspace Orthogonal(Vector3d from_point)
            {
                return WholeSpace.singleton;
            }
        }
    }
}
