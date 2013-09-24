import sublime, sublime_plugin, os, codecs
import shutil
import sys, platform
from os.path import expanduser

# Paths

HOME = expanduser("~")
#PYGIT2_BASEDIR = '/Library/Python/2.7/site-packages/'
PACKAGE_PATH = os.path.dirname(os.path.realpath(__file__))

PYGIT2_BASEDIR = os.path.join(PACKAGE_PATH,'site-packages')

AUTOGIT_PATH = '/usr/local/autogit/'

# libgit2_path = os.getenv("LIBGIT2")
#if libgit2_path is None:
#	if os.name == 'nt':
#   	program_files = os.getenv("ProgramFiles")
#		libgit2_path = '%s\libgit2' % program_files
#	else:
#		libgit2_path = '/usr/local'

# What version are we running?

WinOsVersion,_,_,_ = platform.win32_ver()
MacOsVersion,_,_ = platform.mac_ver()
if( WinOsVersion ):
	APPDIR = os.getenv("APPDATA")
	AUTOGIT_PATH = "%s/autogit/" % APPDIR
	AUTOGIT_PATH = AUTOGIT_PATH.replace("\\","/")


elif( MacOsVersion ):
	AUTOGIT_PATH = "%s/Library/Application Support/autogit/" % HOME

if( not os.path.isdir( AUTOGIT_PATH ) ):
	os.makedirs(AUTOGIT_PATH)

print WinOsVersion, MacOsVersion, AUTOGIT_PATH

# Because sublime uses its own python env, set pygit path manually before loading module:
def fixPath():
	for path in sys.path:
		if path == PYGIT2_BASEDIR:
			return
	sys.path.append(PYGIT2_BASEDIR)
fixPath();

print sys.path

#import pygit2
from dulwich.repo import Repo
from dulwich.index import index_entry_from_stat, changes_from_tree
from dulwich.objects import Blob
from dulwich.diff_tree import tree_changes

class ExampleCommand(sublime_plugin.TextCommand):
	def run(self, edit):
		self.view.insert(edit, 0, "Hello, World!")

class GitRepository():
	def init(self,path):
		#pygit2.init_repository(path, False)
		if( not os.path.isdir( path ) ):
			os.makedirs(path)
		Repo.init(path)

	def adjustPath(self, gitRoot, filePath):
		drive, path = os.path.splitdrive(filePath)
		if drive:
			path = path.replace("\\","/")
		if( path.startswith("/") ):
			path = path[1:]
		joined = os.path.join( gitRoot, path )
		joined = joined.replace("\\","/")
		return joined

	def dulwichCommit(self, filePath, fullPath, kind):

		git = Repo(AUTOGIT_PATH)
		staged = map(str,[filePath])
		git.stage( staged )

		index = git.open_index()

		try:
			committer = git._get_user_identity()
		except ValueError:
			committer = "autogit"

		try:
			head = git.head()
		except KeyError:
			return git.do_commit( '%s - autogit commit (via dulwich)' % kind, committer=committer)

		changes = list(tree_changes(git, index.commit(git.object_store), git['HEAD'].tree))
		if changes and len(changes) > 0:
			return git.do_commit( '%s - autogit commit (via dulwich)' % kind, committer=committer)
		return None

	def pygit2Commit(self, filePath, kind):

		git = pygit2.Repository(GIT_REPOSITORY_PATH)

		index = git.index
		index.read()
		index.add(filePath)
		#oid = index[filePath]
		index.write();
		
		for entry in index:
			print "added %s %s to index" % (entry.path, entry.hex)

		status = git.status()
		try:
			status[filePath]
		except KeyError:
			# If there is nothing different since last save, git status will report no difference.
			return

		try:			
			HEAD = git.revparse_single('HEAD')
			parents = [HEAD.hex]
		except KeyError:
			parents = []

		commit = git.create_commit(
		    'HEAD',
		    pygit2.Signature('autogit you', 'autogit@ninlabs.com'), 
		    pygit2.Signature('autogit you', 'autogit@ninlabs.com'),
		    '%s - autogit commit' % kind,
		    index.write_tree(),
		    parents
		)

		return commit

class AutoGitEvent(sublime_plugin.EventListener):  

	# this will normally not result in a commit unless it was a first time saving file or file was externally modified.
	def on_pre_save(self, view):  
		
		#body = view.substr(sublime.Region(0, view.size())).encode('utf-8')
		#with open(path,'w') as f:
		#	f.write( body )
		#	f.close()

		commit = self.handleCommit(view, "pre save")
		if commit:
			print "*******", view.file_name(), "pre save - commited"
 
	def on_post_save(self, view):  
		#with codecs.open(view.file_name(), "r", "utf-8") as f:
		#	print f.read()
		commit = self.handleCommit(view, "post save")
		if commit:
			print "*******", view.file_name(), "postsave - commited"

	def handleCommit(self,view, kind):
		repo = GitRepository()
		path = repo.adjustPath( AUTOGIT_PATH, view.file_name() )

		dir = os.path.dirname( path )

		if( not os.path.isdir(dir) ):
			os.makedirs(dir)

		shutil.copy2(view.file_name(),path)

		## rel path is needed for commit
		relPath = path.replace(AUTOGIT_PATH, "")
		if( relPath.startswith("/") ):
			relPath = relPath[1:]


		#return repo.commit(relPath, kind)
		return repo.dulwichCommit(relPath, path, kind)

### Create initial autogit repository if it doesn't exist

GIT_REPOSITORY_PATH = os.path.join( AUTOGIT_PATH, ".git" )
if( not os.path.isdir( GIT_REPOSITORY_PATH ) ):
	repo = GitRepository()
	repo.init(AUTOGIT_PATH)
	print "##### created git repo: " + GIT_REPOSITORY_PATH

