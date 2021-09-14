# ProteowizardWrapper

The ProteowizardWrapper is a series of C# classes that can be used to interface with the ProteoWizard libraries.

This code was originally written by Nick Shulman for the MacCoss Lab at the University of Washington in 2009

The original source code to the ProteowizardWrapper is distributed as part of the Proteowizard library.
See folder `pwiz\pwiz_tools\Shared\ProteowizardWrapper`


## Added Functionality

Class MSDataFileReader was added in 2012 to extend class MsDataFileImpl,
adding additional functions for directly accessing the pwiz data objects.

DLL functionality was further extended in 2015 to make the ProteowizardWrapper.dll AnyCPU
and to add logic for finding any missing Proteowizard DLLs by searching standard locations.

## Missing DLL Search Logic

If a required Proteowizard DLL is not found, the following logic is used to locate it:

When the running process is x86:
1) Look for pwiz_bindings_cli.dll in the current working directory
2) Look for a "ProteoWizard_x86" environment variable that points to an existing directory
3) Look for directory "C:\DMS_Programs\ProteoWizard_x86"
4) Look for standard installs in default locations:
   * Look for machine installs:
     * "C:\Program Files (x86)\ProteoWizard\ProteoWizard 3.0.9490" (or different version) on 64-bit Windows
     * "C:\Program Files\ProteoWizard\ProteoWizard 3.0.9490" (or different version)       on 32-bit Windows
   * Look for user installs:
     * "C:\Users\[username]\AppData\Local\Apps\ProteoWizard 3.0.19067.a4153e272 32-bit" (or different 32-bit version)
   * Out of all standard installs found, the most recent version is used

When the running process is x64 (this includes AnyCPU running on 64-bit Windows)
1) Look for pwiz_bindings_cli.dll in the current working directory
2) Look for a "ProteoWizard" environment variable that points to an existing directory
3) Look for directory "C:\DMS_Programs\ProteoWizard"
4) Look for standard installs in default locations:
   * Look for machine installs:
     * "C:\Program Files\ProteoWizard\ProteoWizard 3.0.9490" (or different version)
   * Look for user installs:
     * "C:\Users\[username]\AppData\Local\Apps\ProteoWizard 3.0.19067.a4153e272 64-bit" (or different 64-bit version)
   * Out of all standard installs found, the most recent version is used

Technical note: an AnyCPU program can be compiled with setting "prefer 32-bit" enabled.
In that case, when the AnyCPU program runs on 64-bit windows it will run as an x86 process.

Caution: If pwiz_bindings_cli.dll is present in the same folder as ProteowizardWrapper.dll,
ProteoWizard's DLLs will be presumed to be present in that folder.

## ProteoWizard Info

Proteowizard web pages:
[ProteoWizard Features and Download](http://proteowizard.sourceforge.net/) on SourceForge
[ProteoWizard Reviews and Download](http://sourceforge.net/projects/proteowizard)

Download a Windows installer from:
[ProteoWizard Downloads Page](http://proteowizard.sourceforge.net/downloads.shtml)

Alternatively, to download a .tar.bz2 file with the latest .Exe files:
1) Go to [labkey.org](http://teamcity.labkey.org:8080/project.html?projectId=ProteoWizard) and click "Login as Guest User"
2) Find "Windows x86" in the list
3) If necessary, expand it by clicking the Plus sign
4) Just to the right of the word "Artifacts" is a down arrow; click it to reveal a dropdown list
5) Choose the latest *-bin-windows-x86*.tar.bz2 file
   *  For example, pwiz-bin-windows-x86-vc120-release-3_0_9490.tar.bz2
6) After downloading the file, extract the data using 7-zip (http://www.7-zip.org/) or WinRar

To download the source code:
1) Go to [sourceforge.net repository](https://sourceforge.net/p/proteowizard/code/HEAD/tree/trunk/)
2) Click "Download Snapshot" or use the SVN link to checkout with Subversion

## Contacts

Written by Matthew Monroe and Bryson Gibbons for the Department of Energy (PNNL, Richland, WA) \
Copyright 2017, Battelle Memorial Institute.  All Rights Reserved. \
E-mail: proteomics@pnnl.gov \
Website: https://panomics.pnl.gov/ or https://omics.pnl.gov

## License

Licensed under the Apache License, Version 2.0; you may not use this program except
in compliance with the License.  You may obtain a copy of the License at
http://www.apache.org/licenses/LICENSE-2.0
