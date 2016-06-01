using System.Collections.Generic;
using System.Linq;
using Raml.Parser.Expressions;

namespace Raml.Tools
{
    public class TraitsApplier
    {
        public static void ApplyTraitsToMethods(ICollection<Method> methods, IEnumerable<IDictionary<string, Method>> traits, IEnumerable<string> isArray)
        {
            foreach (var @is in isArray)
            {
                if (traits.Any(t => t.ContainsKey(@is)))
                {
                    var trait = traits.First(t => t.ContainsKey(@is))[@is];
                    ApplyTraitToMethods(methods, trait);
                }
            }
        }

        public static void ApplyTraitsToMethod(Method method, IEnumerable<IDictionary<string, Method>> traits, IEnumerable<string> isArray)
        {
            foreach (var @is in isArray)
            {
                if (traits.Any(t => t.ContainsKey(@is)))
                {
                    var trait = traits.First(t => t.ContainsKey(@is))[@is];
                    ApplyTraitToMethod(method, trait);
                }
            }
        }


        private static void ApplyTraitToMethods(ICollection<Method> methods, Method trait)
        {
            foreach (var method in methods)
            {
                ApplyTraitToMethod(method, trait);
            }
        }

        private static void ApplyTraitToMethod(Method method, Method trait)
        {
            if (trait.BaseUriParameters != null)
                method.BaseUriParameters = trait.BaseUriParameters;

            if (trait.Body != null)
                method.Body = trait.Body;

            if (trait.Headers != null)
                method.Headers = trait.Headers;

            if (trait.Is != null)
                method.Is = trait.Is;

            if (trait.Protocols != null)
                method.Protocols = trait.Protocols;

            if (trait.QueryParameters != null)
                method.QueryParameters = trait.QueryParameters;

            if (trait.Responses != null)
                method.Responses = trait.Responses;

            if (trait.SecuredBy != null)
                method.SecuredBy = trait.SecuredBy;

            //method.Verb = trait.Verb;
        }
    }
}