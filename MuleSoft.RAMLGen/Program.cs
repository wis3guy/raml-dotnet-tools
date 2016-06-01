using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CommandLine;
using Raml.Common;
using Raml.Parser;
using Raml.Tools.ClientGenerator;


namespace MuleSoft.RAMLGen
{
    class Program
    {
        static int Main(string[] args)
        {
            Parser.Default.ParseArguments<ClientOptions, ServerOptions, string>(args)
                .MapResult(
                    (ClientOptions opts) => RunReferenceAndReturnExitCode(opts),
                    (ServerOptions opts) => RunContractAndReturnExitCode(opts),
                    errors => HandleError(errors, args));

            Console.WriteLine("The code was generated successfully");
            return 0;
        }

        private static int HandleError(IEnumerable<Error> errors, string[] args)
        {
            //if (args.Any(a => a.ToLowerInvariant() == "--help" || a.ToLowerInvariant() == "help"))
            //    return 0;

            //foreach (var error in errors)
            //{
            //    Console.WriteLine(Enum.GetName(typeof(ErrorType), error.Tag));
            //    var namedError = error as NamedError;
            //    if (namedError != null)
            //    {
            //        Console.WriteLine(namedError.NameInfo.LongName);
            //        Console.WriteLine(namedError.NameInfo.NameText);
            //    }
            //}
            return 0;
        }


        private static int RunContractAndReturnExitCode(ServerOptions opts)
        {
            var generator = new RamlGenerator();
            generator.HandleContract(opts).ConfigureAwait(false).GetAwaiter().GetResult();
            return 0;
        }


        private static int RunReferenceAndReturnExitCode(ClientOptions opts)
        {
            var generator = new RamlGenerator();
            generator.HandleReference(opts).ConfigureAwait(false).GetAwaiter().GetResult(); ;
            return 0;
        }

    }
}
