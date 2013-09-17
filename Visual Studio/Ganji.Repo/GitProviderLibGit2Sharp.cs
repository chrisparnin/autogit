using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibGit2Sharp;
using System.Security.Principal;
using System.Reflection;
using System.IO;
using System.Diagnostics;

namespace Ganji.Repo
{
    // Generate a private/public key
    // http://www.codeguru.com/csharp/.net/net_general/article.php/c4643/Giving-a-NET-Assembly-a-Strong-Name.htm

    // Sign LibGit2Sharp
    // http://stackoverflow.com/questions/1379954/how-i-do-a-sign-an-assembly-that-has-already-been-built-into-a-dll-specifically
    /*
        From a VS.NET command prompt, enter the following:

        Generate a KeyFile: sn -k keyPair.snk
        Obtain the MSIL for the provided assembly: ildasm providedAssembly.dll /out:providedAssembly.il
        Rename/move the original assembly: ren providedAssembly.dll providedAssembly.dll.orig
        Create a new assembly from the MSIL output and your assembly KeyFile: ilasm providedAssembly.il /dll /key= keyPair.snk
    */

    public class GitProviderLibGit2Sharp
    {
        public string SolutionBaseDirectory
        {
            get;
            set;
        }
        public string ContextRepository
        {
            get;
            set;
        }
        // Good sample
        // https://github.com/libgit2/libgit2sharp/blob/master/LibGit2Sharp.Tests/CommitFixture.cs

        private void SetEnvironmentPath()
        {
            string originalAssemblypath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;

            //TODO: When amd64 version of libgit2.dll is available, value this depending of the size of an IntPtr
            //const string currentArchSubPath = "/x86";

            string path = Path.Combine(Path.GetDirectoryName(originalAssemblypath), "NativeBinaries", "x86");

            const string pathEnvVariable = "PATH";
            Environment.SetEnvironmentVariable(pathEnvVariable,
                                               String.Format("{0}{1}{2}", path, Path.PathSeparator, Environment.GetEnvironmentVariable(pathEnvVariable)));
        }

        public void Init(string path)
        {
            System.IO.Directory.CreateDirectory(path);

            try
            {
                //Assembly.LoadFile(@"\\psf\Home\Desktop\repo\code\trunk\Ganji\Ganji.Repo.Tests\bin\Debug\LibGit2Sharp.dll");
                //SetEnvironmentPath();

                Repository.Init(path, false);
                {
                    //System.IO.Directory.CreateDirectory(System.IO.Path.Combine(path, "saves"));
                    //System.IO.Directory.CreateDirectory(System.IO.Path.Combine(path, "builds"));
                    //System.IO.Directory.CreateDirectory(System.IO.Path.Combine(path, "days"));
                }
            }
            catch( Exception ex )
            {
                if (ex.InnerException != null
                    && ex.InnerException.Message.StartsWith( "Unable to load DLL 'git2':"))
                {
                    throw new Exception(
                        @"""
                        When referencing Ganji.Repo, you must also add the following PostBuild Step to the referencing project:

                        if not exist ""$(TargetDir)NativeBinaries"" md ""$(TargetDir)NativeBinaries""
                        if not exist ""$(TargetDir)NativeBinaries\x86"" md ""$(TargetDir)NativeBinaries\x86""
                        xcopy /s /y ""$(SolutionDir)packages\LibGit2Sharp.0.8\NativeBinaries\x86\*.*"" ""$(TargetDir)NativeBinaries\x86""
                        if not exist ""$(TargetDir)NativeBinaries\amd64"" md ""$(TargetDir)NativeBinaries\amd64""
                        xcopy /s /y ""$(SolutionDir)packages\LibGit2Sharp.0.8\NativeBinaries\amd64\*.*"" ""$(TargetDir)NativeBinaries\amd64""
                    
                        Alternatively, 

                        Select the Project Reference node to 'Ganji.Repo' in Solution Explorer and view the Properties (F4)
                        Change the value for ""Output Groups Included in VSIX"" to the following: BuiltProjectOutputGroup;BuiltProjectOutputGroupDependencies;GetCopyToOutputDirectoryItems;SatelliteDllsProjectOutputGroup

                        http://stackoverflow.com/questions/6974244/vsix-package-doesnt-include-localized-resources-of-referenced-assembly
                        """
                        );
                }
            }
        }

        string RepoPath;
        string UserName;
        public void Open(string path)
        {
            this.RepoPath = path;
            this.ContextRepository = RepoPath;
            this.UserName = WindowsIdentity.GetCurrent().Name;
        }

        public bool CopyFileToCache(string file)
        {
            using (Repository repo = new Repository(RepoPath))
            {
                var newPath = PrepareDocumentCache(file, ContextRepository);
                System.IO.File.Copy(file, newPath, true);

                repo.Index.Stage(newPath);

                var status = repo.Index.RetrieveStatus(newPath);
                if (status == FileStatus.Unaltered)
                {
                    // file was unchanged...can skip commit.
                    return false;
                }
                return true;

            }
        }

        public string Commit(string kind)
        {
            if( string.IsNullOrEmpty( RepoPath ) )
                throw new ArgumentException("Must open first");

            using( Repository repo = new Repository(RepoPath) )
            {
                var author = new Signature(UserName, UserName, DateTimeOffset.Now);
                var commit = repo.Commit( string.Format("Ganji commit - %s", kind), author, author);
                return commit.Id.Sha;
            }
        }

        public string ReadCommit(string commitId, string fileName)
        {
            try
            {
                string relativePath = GetRelativeName(fileName);
                using (Repository repo = new Repository(RepoPath))
                {
                    var commit = repo.Lookup<Commit>(commitId);
                    if (commit == null)
                        return null;
                    var blob = commit[relativePath];
                    if( blob == null )
                        return null;
                    var blobTarget = blob.Target as Blob;
                    return blobTarget.ContentAsUtf8();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
            return null;
        }

        private string PrepareDocumentCache(string file, string repoPath)
        {
            string relativePath = GetRelativeName(file);
            var newPath = System.IO.Path.Combine(repoPath, relativePath);
            var dirPath = System.IO.Path.GetDirectoryName(newPath);
            if (!System.IO.File.Exists(dirPath))
            {
                // This will create subdirectories too if missing...
                System.IO.Directory.CreateDirectory(dirPath);
            }
            return newPath;
        }

        private string GetGitFriendlyName(string file)
        {
            var relative = GetRelativeName(file);
            var parts = relative.Split(System.IO.Path.DirectorySeparatorChar);
            return string.Join("/", parts);
        }

        private string GetRelativeName(string file)
        {
            var dir = SolutionBaseDirectory.EndsWith("\\") ? SolutionBaseDirectory : SolutionBaseDirectory + "\\";
            return file.Replace(dir, "");
        }
    }
}
