using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Ganji.Repo;

namespace ninlabsresearch.autogit
{
    class SaveListener : IVsRunningDocTableEvents3
    {
        GitProviderLibGit2Sharp provider;
        IVsRunningDocumentTable m_RDT;
        uint m_rdtCookie = 0;
        public bool Register(EnvDTE.DTE dte, string repoPath, string solutionPath)
        {
            // Register events for running document table.
            m_RDT = (IVsRunningDocumentTable)Package.GetGlobalService(typeof(SVsRunningDocumentTable));
            m_RDT.AdviseRunningDocTableEvents(this, out m_rdtCookie);

            provider = new GitProviderLibGit2Sharp();
            provider.ContextRepository = repoPath;
            provider.SolutionBaseDirectory = solutionPath;
            provider.Open(repoPath);

            // I: test if this table is from multiple instances routed here...
            return true;
        }

        public void Shutdown()
        {
            if (m_RDT != null)
            {
                m_RDT.UnadviseRunningDocTableEvents(m_rdtCookie);
                m_RDT = null;
            }
        }

        // IVsRunningDocTableEvents3 Members
        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }

        // renames...
        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            //// look up pszMkDocumentOld
            //// update record to be with pszMkDocumentNew
            //if (pszMkDocumentOld != pszMkDocumentNew)
            //{
            //    m_docContext.UpdateRenamedDocument(pszMkDocumentOld, pszMkDocumentNew, m_database);
            //}
            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        Dictionary<uint, DateTime> saveTimes = new Dictionary<uint, DateTime>();
        public int OnBeforeSave(uint docCookie)
        {
            uint flags, readlocks, editlocks;
            string name; IVsHierarchy hier;
            uint itemid; IntPtr docData;
            m_RDT.GetDocumentInfo(docCookie, out flags, out readlocks, out editlocks, out name, out hier, out itemid, out docData);

            // Should this only be done first time document is made?  Or is just good practice since there can be other external changes...merging, refactoring...
            HandleSave(name, "pre save");

            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            // Don't need copy as long as can compare current version...

            // However, going to mark DocumentTag with after...
            uint flags, readlocks, editlocks;
            string name; IVsHierarchy hier;
            uint itemid; IntPtr docData;
            m_RDT.GetDocumentInfo(docCookie, out flags, out readlocks, out editlocks, out name, out hier, out itemid, out docData);


            HandleSave(name, "post save");
            return VSConstants.S_OK;
        }

        private void HandleSave(string name, string kind)
        {
            try
            {
                // add file to commit!
                if (provider.CopyFileToCache(name))
                {
                    // commit to git...
                    var commitId = provider.Commit(kind);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }
    }
}
