The ProteowizardWrapper is a series of C# classes that can be used to interface with the ProteoWizard libraries.

This code was originally written by Nick Shulman for the MacCoss Lab at the University of Washington in 2009

The original source code to the ProteowizardWrapper is distributed as part of the Proteowizard library.
It is located at:
  pwiz\pwiz_tools\Shared\ProteowizardWrapper


== Added Functionality ==

Class MSDataFileReader was added in 2012 to extend class MsDataFileImpl, 
adding additional functions for directly accessing the pwiz data objects.

DLL functionality was further extended in 2015 to make the ProteowizardWrapper.dll AnyCPU 
and to add logic for finding any missing Proteowizard DLLs by searching standard locations.


== Missing DLL Search Logic ==

If a required Proteowizard DLL is not found, the following logic is used to locate it:

When the running process is x86:
1) Look in the current working directory
2) Look for a "ProteoWizard_x86" environment variable that points to an existing directory
3) Look for directory "C:\DMS_Programs\ProteoWizard_x86"
4) Look for a directory with a name like
   "C:\Program Files (x86)\ProteoWizard\ProteoWizard 3.0.9490" on 64-bit Windows, or
   "C:\Program Files\ProteoWizard\ProteoWizard 3.0.9490"       on 32-bit Windows

When the running process is x64 (this includes AnyCPU running on 64-bit Windows)
1) Look in the current working directory
2) Look for a "ProteoWizard" environment variable that points to an existing directory
3) Look for directory "C:\DMS_Programs\ProteoWizard"
4) Look for a directory with a name like "C:\Program Files\ProteoWizard\ProteoWizard 3.0.9490"

Technical note: an AnyCPU program can be compiled with setting "prefer 32-bit" enabled.
In that case, when the AnyCPU program runs on 64-bit windows it will run as an x86 process.


== ProteoWizard Info ==

Proteowizard web pages:
http://proteowizard.sourceforge.net/
http://sourceforge.net/projects/proteowizard

Download a Windows installer from:
http://proteowizard.sourceforge.net/downloads.shtml

Alternatively, to download a .tar.bz2 file with the latest .Exe files:
1) Go to http://teamcity.labkey.org:8080/project.html?projectId=ProteoWizard and click "Login as Guest User"
2) Find "Windows x86" in the list
3) If necessary, expand it by clicking the Plus sign
4) Just to the right of the word "Artifacts" is a down arrow; click it to reveal a dropdown list
5) Choose the latest *-bin-windows-x86*.tar.bz2 file
   For example, pwiz-bin-windows-x86-vc120-release-3_0_9490.tar.bz2
6) After downloading the file, extract the data using 7-zip (http://www.7-zip.org/) or WinRar

To download the source code:
1) Go to https://sourceforge.net/p/proteowizard/code/HEAD/tree/trunk/
2) Click "Download Snapshot" or use the SVN link to checkout with Subversion
