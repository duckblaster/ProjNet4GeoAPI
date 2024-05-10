using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ProjNet.CoordinateSystems.Transformations;

namespace ProjNet.CoordinateSystems.Projections
{
    /// <summary>
    /// Registry class for all known <see cref="MapProjection"/>s.
    /// </summary>
    public class ProjectionsRegistry
    {
        private static readonly Dictionary<string, Type> TypeRegistry = new Dictionary<string, Type>();
        private static readonly Dictionary<string, Type> ConstructorRegistry = new Dictionary<string, Type>();

        private static readonly object RegistryLock = new object();

        /// <summary>
        /// Static constructor
        /// </summary>
        static ProjectionsRegistry()
        {
            Register("mercator", typeof(Mercator));
            Register("mercator_1sp", typeof (Mercator));
            Register("mercator_2sp", typeof (Mercator));
            Register("pseudo-mercator", typeof(PseudoMercator));
            Register("popular_visualisation pseudo-mercator", typeof(PseudoMercator));
            Register("google_mercator", typeof(PseudoMercator));
            
            Register("transverse_mercator", typeof(TransverseMercator));
            Register("gauss_kruger", typeof(TransverseMercator));

            Register("albers", typeof(AlbersProjection));
            Register("albers_conic_equal_area", typeof(AlbersProjection));

            Register("krovak", typeof(KrovakProjection));

            Register("polyconic", typeof(PolyconicProjection));
            
            Register("lambert_conformal_conic", typeof(LambertConformalConic2SP));
            Register("lambert_conformal_conic_2sp", typeof(LambertConformalConic2SP));
            Register("lambert_conic_conformal_(2sp)", typeof(LambertConformalConic2SP));

            Register("lambert_azimuthal_equal_area", typeof(LambertAzimuthalEqualAreaProjection));

            Register("cassini_soldner", typeof(CassiniSoldnerProjection));
            Register("hotine_oblique_mercator", typeof(HotineObliqueMercatorProjection));
            Register("hotine_oblique_mercator_azimuth_center", typeof(HotineObliqueMercatorProjection));
            Register("oblique_mercator", typeof(ObliqueMercatorProjection));
            Register("oblique_stereographic", typeof(ObliqueStereographicProjection));
            Register("orthographic", typeof(OrthographicProjection));
        }

        /// <summary>
        /// Method to register a new Map
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        public static void Register(string name,
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        Type type)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!typeof(MathTransform).IsAssignableFrom(type))
                throw new ArgumentException("The provided type does not implement 'GeoAPI.CoordinateSystems.Transformations.IMathTransform'!", nameof(type));

            var ci = CheckConstructor(type);
            if (ci == null)
                throw new ArgumentException("The provided type is lacking a suitable constructor", nameof(type));

            string key = ProjectionNameToRegistryKey(name);
            lock (RegistryLock)
            {
                if (TypeRegistry.ContainsKey(key))
                {
                    var rt = TypeRegistry[key];
                    if (ReferenceEquals(type, rt))
                        return;
                    throw new ArgumentException("A different projection type has been registered with this name", "name");
                }

                TypeRegistry.Add(key, type);
                ConstructorRegistry.Add(key, ci);
            }
        }

        private static string ProjectionNameToRegistryKey(string name)
        {
            return name.ToLowerInvariant().Replace(' ', '_');
        }

        /// <summary>
        /// Register an alias for an existing Map.
        /// </summary>
        /// <param name="aliasName"></param>
        /// <param name="existingName"></param>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2067",
            Justification = "The list only contains types stored through the annotated setter.")]
#endif
        public static void RegisterAlias(string aliasName, string existingName)
        {
            lock (RegistryLock)
            {

                if (!TypeRegistry.TryGetValue(ProjectionNameToRegistryKey(existingName), out var existingProjectionType))
                {
                    throw new ArgumentException($"{existingName} is not a registered projection type");
                }

                Register(aliasName, existingProjectionType);
            }
        }

        private static Type CheckConstructor(
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        Type type)
        {
            // find a constructor that accepts exactly one parameter that's an
            // instance of List<ProjectionParameter>, and then return the exact
            // parameter type so that we can create instances of this type with
            // minimal copying in the future, when possible.
            foreach (var c in type.GetConstructors())
            {
                var parameters = c.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(typeof(List<ProjectionParameter>)))
                {
                    return parameters[0].ParameterType;
                }
            }

            return null;
        }

#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2067",
            Justification = "The list only contains types stored through the annotated setter.")]
#endif
        internal static MathTransform CreateProjection(string className, IEnumerable<ProjectionParameter> parameters)
        {
            string key = ProjectionNameToRegistryKey(className);

            Type projectionType;
            Type ci;

            lock (RegistryLock)
            {
                if (!TypeRegistry.TryGetValue(key, out projectionType))
                    throw new NotSupportedException($"Projection {className} is not supported.");
                ci = ConstructorRegistry[key];
            }

            if (!ci.IsInstanceOfType(parameters))
            {
                parameters = new List<ProjectionParameter>(parameters);
            }

            var res = (MapProjection)Activator.CreateInstance(projectionType, parameters);
            if (!res.Name.Equals(className, StringComparison.InvariantCultureIgnoreCase))
            {
                res.Alias = res.Name;
                res.Name = className;
            }
            return res;
        }
    }
}
