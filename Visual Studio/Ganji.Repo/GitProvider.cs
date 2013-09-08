using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using NGit;
using NGit.Treewalk;
using NGit.Revwalk;
using NGit.Dircache;
using NGit.Treewalk.Filter;

namespace Ganji.Repo
{
    [Obsolete]
    class GitProvider
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

        NGit.Api.Git m_git;
        public void Init(string path)
        {
            NGit.Api.InitCommand init = new NGit.Api.InitCommand();

            System.IO.Directory.CreateDirectory(path);

            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(path, "saves"));
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(path, "builds"));
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(path, "days"));
            init.SetDirectory(new Sharpen.FilePath(path));
            m_git = init.Call();
        }

        public string ReadCommit(string commitId, string fileName)
        {
            var repo = m_git.GetRepository();
            var id = ObjectId.FromString(commitId);

            RevCommit commit = null;
            try
            {
                commit = ParseCommit(repo, id);
                if (commit == null)
                    return null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
                return null;
            }
            //var commits = m_git.Log().AddRange(id, id).Call();
            //var commit = commits.SingleOrDefault();
            //if (commit == null)
            //    return null;

            TreeWalk walk = new TreeWalk(repo);
            //RevWalk r = new RevWalk(m_git.GetRepository());
            //var tree = r.ParseTree(commit.Tree.Id);
            //r.LookupTree(
            //walk.AddTree(new FileTreeIterator(repo));

            //var tree = ParseTree(repo, commit.Tree.Id);
            walk.AddTree(commit.Tree);
            var filter = GetGitFriendlyName(fileName);
            walk.Filter = PathFilterGroup.CreateFromStrings(new string[]{filter});
            //walk.EnterSubtree();
            while (walk.Next())
            {
                var path = walk.PathString;
                if (walk.IsSubtree)
                {
                    walk.EnterSubtree();
                    continue;
                }
                if (path == filter)
                {
                    var cur = walk.GetObjectId(0);
                    ObjectLoader ol = repo.Open(cur);

                    //    //Console.WriteLine(string.Format("Path: {0}{1}", walk.PathString, walk.IsSubtree ? "/" : ""));
                    //    //var loader = reader.Open(commit.Tree.Id);
                    var text = "";
                    using (var stream = ol.OpenStream())
                    using (var sr = new System.IO.StreamReader(stream))
                    {
                        text = sr.ReadToEnd();
                    }
                    return text;
                }
            }
            //walk.Reset();
            //reader.Open();
            return "";
        }

        private RevCommit ParseCommit(Repository repo, ObjectId id)
        {
            RevWalk rw = new RevWalk(repo);
            var head = rw.ParseCommit(id);
            rw.MarkStart(head);
            RevCommit commit = null;
            try
            {
                commit = rw.Next();
                while( commit != null )
                {
                    if (commit.Id.Name == id.Name)
                    {
                        return commit;
                    }
                    commit = rw.Next();
                }

            }
            finally
            {
                rw.Release();
            }
            return commit;
        }

        private RevTree ParseTree(Repository repo, ObjectId id)
        {
            RevWalk rw = new RevWalk(repo);
            
            RevTree tree;
            try
            {
                tree = rw.LookupTree(id);
            }
            finally
            {
                rw.Release();
            }
            return tree;
        }


        public void Open(string path)
        {
            var p = new Sharpen.FilePath(path);
            try
            {
                m_git = NGit.Api.Git.Open(p);
            }
            catch (Exception ex)
            { 
            }
            if (m_git == null)
            {
                Init(path);
            }

        }

        public string Commit()
        {
            if (m_git == null)
                throw new ArgumentException();

            try
            {
                var rev = m_git.Commit().SetMessage("Ganji autocommit").Call();
                return rev.Id.Name;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
            return "";
        }

        private string GetRelativeName(string file)
        {
            var dir = SolutionBaseDirectory.EndsWith("\\") ? SolutionBaseDirectory : SolutionBaseDirectory + "\\";
            return file.Replace(dir, "");
        }

        private string GetGitFriendlyName(string file)
        {
            var relative = GetRelativeName(file);
            var parts = relative.Split(System.IO.Path.DirectorySeparatorChar);
            return string.Join("/", parts);
        }

        public void CopyFileToCache(string file)
        {
            try
            {
                var newPath = PrepareDocumentCache(file, ContextRepository);
                System.IO.File.Copy(file, newPath, true);
                //m_git.Add().AddFilepattern(tuple.Item1).Call();
                //m_git.Add().AddFilepattern(".").Call();

                var command = m_git.Add();
                var filter = GetGitFriendlyName(file);

                command.AddFilepattern(filter).Call();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        private string PrepareDocumentCache(string file, string repoPath)
        {
            string relativePath = GetRelativeName(file);
            var newPath = System.IO.Path.Combine(repoPath, relativePath );
            var dirPath = System.IO.Path.GetDirectoryName(newPath);
            if (!System.IO.File.Exists(dirPath))
            {
                // This will create subdirectories too if missing...
                System.IO.Directory.CreateDirectory(dirPath);
            }
            return newPath;
        }

    }
}
