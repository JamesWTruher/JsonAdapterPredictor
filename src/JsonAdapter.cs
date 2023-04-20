using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Feedback;
using System.Management.Automation.Subsystem.Prediction;

namespace JsonAdapterProvider
{
    public sealed class Init : IModuleAssemblyInitializer, IModuleAssemblyCleanup
    {
        internal const string id = "6edf7436-db79-4b5b-b889-4e6d6a1c8680";

        public void OnImport()
        {
            SubsystemManager.RegisterSubsystem<IFeedbackProvider, JsonAdapterFeedbackPredictor>(JsonAdapterFeedbackPredictor.Singleton);
            SubsystemManager.RegisterSubsystem<ICommandPredictor, JsonAdapterFeedbackPredictor>(JsonAdapterFeedbackPredictor.Singleton);
        }

        public void OnRemove(PSModuleInfo psModuleInfo)
        {
            SubsystemManager.UnregisterSubsystem<IFeedbackProvider>(new Guid(id));
            SubsystemManager.UnregisterSubsystem<ICommandPredictor>(new Guid(id));
        }
    }

    public sealed class JsonAdapterFeedbackPredictor : IFeedbackProvider, ICommandPredictor
    {
        private readonly Guid _guid;
        private string? _suggestion;
        private Runspace _rs;
        private CommandInvocationIntrinsics _cIntrinsics;
        Dictionary<string, string>? ISubsystem.FunctionsToDefine => null;

        private int accepted = 0;
        private int displayed = 0;
        private int executed = 0;

        public static JsonAdapterFeedbackPredictor Singleton { get; } = new JsonAdapterFeedbackPredictor(Init.id);
        private JsonAdapterFeedbackPredictor(string guid)
        {
            _guid = new Guid(guid);
            _rs = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault());
            _rs.Open();
            _cIntrinsics = _rs.SessionStateProxy.InvokeCommand;
        }

        public void Dispose()
        {
            _rs.Dispose();
        }

        public Guid Id => _guid;

        public string Name => "JsonAdapter";

        public string Description => "Finds a JSON adapter for a native application.";

        /// <summary>
        /// Gets feedback based on the given command line and error record.
        /// </summary>
        public FeedbackItem? GetFeedback(string commandLine, ErrorRecord lastError, CancellationToken token)
        {
            List<string> pipelineElements = new List<string>();
            Ast myAst = Parser.ParseInput(commandLine, out _, out _);
            bool adapterFound = false;
            var allowedAdapterTypes = CommandTypes.Application |
                CommandTypes.Function |
                CommandTypes.Filter |
                CommandTypes.Alias |
                CommandTypes.ExternalScript |
                CommandTypes.Script;

            foreach(var cAst in myAst.FindAll((ast) => ast is CommandAst, true))
            {
                var commandAst = (CommandAst)cAst;
                var commandName = commandAst.GetCommandName();
                if (commandName is null)
                {
                    continue;
                }

                var command = _cIntrinsics.GetCommand(commandName, CommandTypes.Application);
                if (command is null)
                {
                    continue;
                }

                var adapterCmd = string.Format("{0}-json", commandName);
                var adapter = _cIntrinsics.GetCommand(adapterCmd, allowedAdapterTypes);
                if (adapter is null)
                {
                    pipelineElements.Add(cAst.Extent.Text);
                    continue;
                }

                pipelineElements.Add(string.Format("{0} | {1}", cAst.Extent.Text, adapterCmd));
                adapterFound = true;
            }

            if (!adapterFound)
            {
                return null;
            }

            // Rewrite the command line to use the adapter
            var pipeline = string.Join(" | ", pipelineElements);
            return new FeedbackItem(
                Name,
                new List<string> { pipeline }
                );

        }

        public bool CanAcceptFeedback(PredictionClient client, PredictorFeedbackKind feedback)
        {
            return feedback switch
            {
                PredictorFeedbackKind.CommandLineAccepted => true,
                _ => false,
            };
        }

        public SuggestionPackage GetSuggestion(PredictionClient client, PredictionContext context, CancellationToken cancellationToken)
        {
            List<PredictiveSuggestion>? result = null;

            result ??= new List<PredictiveSuggestion>(1);
            if (_suggestion is null)
            {
                return default;
            }

            result.Add(new PredictiveSuggestion(_suggestion));

            if (result is not null)
            {
                return new SuggestionPackage(result);
            }

            return default;
        }

        public void OnCommandLineAccepted(PredictionClient client, IReadOnlyList<string> history)
        {
            _suggestion = null;
        }

        public void OnSuggestionDisplayed(PredictionClient client, uint session, int countOrIndex) {
            displayed++;
        }

        public void OnSuggestionAccepted(PredictionClient client, uint session, string acceptedSuggestion) {
            accepted++;
         }

        public void OnCommandLineExecuted(PredictionClient client, string commandLine, bool success) {
            executed++;
        }
    }
}
