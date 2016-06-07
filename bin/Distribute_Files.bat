@echo off
echo Copying the DLL
@echo on

xcopy Release\ProteowizardWrapper.dll "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\Lib\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\Release\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\Release\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\UnitTests\bin\Debug\" /D /Y

xcopy Release\ProteowizardWrapper.pdb "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\Lib\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\Release\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\Release\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\UnitTests\bin\Debug\" /D /Y


xcopy Release\ProteowizardWrapper.dll "F:\My Documents\Projects\GordonSlysz\DeconTools_IQ\Library\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\My Documents\Projects\GordonSlysz\DeconTools_IQ\DeconConsole\bin\x64\Debug\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\My Documents\Projects\GordonSlysz\DeconTools_IQ\DeconConsole\bin\x86\Debug\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\My Documents\Projects\GordonSlysz\DeconTools_IQ\DeconConsole\bin\x86\Release\" /D /Y

xcopy Release\ProteowizardWrapper.dll "F:\My Documents\Projects\Instrument-Software\LCMS-Spectator\Library\" /D /Y

@echo off
pause