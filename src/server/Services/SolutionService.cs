using System.Reflection;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace dotRush.Server.Services {

    public class SolutionService {
        public static SolutionService? Instance { get; private set; }
        public Solution? CurrentSolution { get; private set; }
        public string? Target { get; private set; }

        private SolutionService() {}

        public static async Task Initialize(string target) {
            Instance = new SolutionService();
            Instance.Target = target;

            var queryOptions = VisualStudioInstanceQueryOptions.Default;
            var instances = MSBuildLocator.QueryVisualStudioInstances(queryOptions);
            MSBuildLocator.RegisterInstance(instances.FirstOrDefault());

            var workspace = MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            await workspace.OpenSolutionAsync(target);

            Instance.CurrentSolution = workspace.CurrentSolution;

            foreach (var project in Instance.CurrentSolution.Projects) {
                await project.GetCompilationAsync();
            }

            // workspace.WorkspaceChanged += (sender, args) => {
            //     if (args.Kind == WorkspaceChangeKind.SolutionAdded) {
            //         Instance!.CurrentSolution = args.NewSolution;
            //     }
            // };

        }

        public void UpdateSolution(Solution solution) {
            CurrentSolution = solution;
        }
    }
}