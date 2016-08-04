using System;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;

namespace Raml.Common
{
    public class VisualStudioAutomationHelper
    {
        public const string RamlVsToolsActivityLogSource = "RamlVsTools";

        public static Project GetActiveProject(_DTE dte)
        {
            Project activeProject = null;

            var activeSolutionProjects = dte.ActiveSolutionProjects as Array;
            if (activeSolutionProjects != null && activeSolutionProjects.Length > 0)
            {
                activeProject = activeSolutionProjects.GetValue(0) as Project;
            }

            return activeProject;
        }

        public static string GetDefaultNamespace(IServiceProvider serviceProvider)
        {
            var dte = serviceProvider.GetService(typeof(SDTE)) as DTE;
            var project = GetActiveProject(dte);

            var namespaceProperty = VisualStudioAutomationHelper.IsAVisualStudio2015Project(project) ?  "RootNamespace" : "DefaultNamespace";
            return project.Properties.Item(namespaceProperty).Value.ToString();
        }

        public static string GetExceptionInfo(Exception ex)
        {
            return ex.Message + Environment.NewLine + ex.StackTrace +
                   (ex.InnerException != null
                       ? Environment.NewLine + ex.InnerException.Message + Environment.NewLine +
                         ex.InnerException.StackTrace
                       : string.Empty);
        }

        public static ProjectItem AddFolderIfNotExists(Project proj, string folderName)
        {
            var path = Path.GetDirectoryName(proj.FullName) + "\\" + folderName + "\\";
            var projectItem = proj.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == folderName);
            if (projectItem != null)
                return projectItem;

            if (!IsAVisualStudio2015Project(proj) && Directory.Exists(path))
                projectItem = proj.ProjectItems.AddFromDirectory(path);
            else if(!Directory.Exists(path))
                projectItem = proj.ProjectItems.AddFolder(folderName);

            return projectItem;
        }

        public static ProjectItem AddFolderIfNotExists(ProjectItem projItem, string folderName)
        {
            var path = Path.GetDirectoryName(projItem.FileNames[0]) + "\\" + folderName + "\\";
            var projectItem = projItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == folderName);
            if (projectItem != null)
                return projectItem;

            if (!IsAVisualStudio2015Project(projItem.ContainingProject) && Directory.Exists(path))
                projectItem = projItem.ProjectItems.AddFromDirectory(path);
            else if (!Directory.Exists(path))
                projectItem = projItem.ProjectItems.AddFolder(folderName);

            return projectItem;
        }


        public static ProjectItem AddFolderIfNotExists(ProjectItem projItem, string folderName, string folderPath)
        {
            
            var projectItem = projItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == folderName);
            if (projectItem != null)
                return projectItem;

            if (Directory.Exists(folderPath))
                projectItem = projItem.ProjectItems.AddFromDirectory(folderPath);
            else
                projectItem = projItem.ProjectItems.AddFolder(folderName);

            return projectItem;
        }

        public static bool IsAVisualStudio2015Project(Project proj)
        {
            if (proj.FileName.EndsWith("xproj"))
                return true;
            return false;
        }


    }
}