using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Ganji.Repo;

namespace ninlabsresearch.autogit
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidautogitPkgString)]
    // VSContants.UICONTEXT_SolutionExists
    [ProvideAutoLoad("f1536ef8-92ec-443c-9ed7-fdadf150da82")]
    public sealed class autogitPackage : Package, IVsSolutionEvents
    {
        private uint m_solutionCookie = 0;
        public EnvDTE.DTE m_dte { get; set; }

        SaveListener m_saveListener;

        public autogitPackage()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
        }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine (string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            //OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            //if ( null != mcs )
            //{
            //    // Create the command for the menu item.
            //    CommandID menuCommandID = new CommandID(GuidList.guidautogitCmdSet, (int)PkgCmdIDList.autogitOptionsCommand);
            //    MenuCommand menuItem = new MenuCommand(MenuItemCallback, menuCommandID );
            //    mcs.AddCommand( menuItem );
            //}

            IVsSolution solution = (IVsSolution)GetService(typeof(SVsSolution));
            ErrorHandler.ThrowOnFailure(solution.AdviseSolutionEvents(this, out m_solutionCookie));
        }

        /// <summary>
        /// This function is the callback used to execute a command when the a menu item is clicked.
        /// See the Initialize method to see how the menu item is associated to this function using
        /// the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            // Show a Message Box to prove we were here
            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            Guid clsid = Guid.Empty;
            int result;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
                       0,
                       ref clsid,
                       "autogit",
                       string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.ToString()),
                       string.Empty,
                       0,
                       OLEMSGBUTTON.OLEMSGBUTTON_OK,
                       OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                       OLEMSGICON.OLEMSGICON_INFO,
                       0,        // false
                       out result));
        }


        #region IVsSolutionEvents Members

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            InitializeWithDTEAndSolutionReady();
            return VSConstants.S_OK;
        }

        private void InitializeWithDTEAndSolutionReady()
        {
            m_dte = (EnvDTE.DTE)this.GetService(typeof(EnvDTE.DTE));

            if (m_dte == null)
                ErrorHandler.ThrowOnFailure(1);

            var solutionBase = "";
            var solutionName = "";
            if (m_dte.Solution != null)
            {
                solutionBase = System.IO.Path.GetDirectoryName(m_dte.Solution.FullName);
                solutionName = System.IO.Path.GetFileNameWithoutExtension(m_dte.Solution.FullName);
            }
            //string dbName = string.Format("Ganji.History-{0}.sdf", solutionName);

            var basePath = PreparePath();
            var repositoryPath = System.IO.Path.Combine(basePath, "LocalHistory");
            var solutionPath = solutionBase;

            m_saveListener = new SaveListener();
            m_saveListener.Register(m_dte, repositoryPath, solutionPath);
        }

        private string PreparePath()
        {
            var basePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            if (m_dte.Solution != null)
            {
                basePath = System.IO.Path.GetDirectoryName(m_dte.Solution.FullName);
            }
            basePath = System.IO.Path.Combine(basePath, ".HistoryData");
            if (!System.IO.Directory.Exists(basePath))
            {
                var info = System.IO.Directory.CreateDirectory(basePath);
                info.Attributes |= System.IO.FileAttributes.Hidden;
            }

            // Also prepare save/build data
            var contextPath = System.IO.Path.Combine(basePath, "LocalHistory");

            if (!System.IO.Directory.Exists(contextPath))
            {
                var provider = new GitProviderLibGit2Sharp();
                provider.Init(contextPath);
            }

            return basePath;
        }


        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseSolution(object pUnkReserved)
        {
            if (m_saveListener != null)
            {
                m_saveListener.Shutdown();
                m_saveListener = null;
            }

            return VSConstants.S_OK;
        }

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        #endregion

    }
}
