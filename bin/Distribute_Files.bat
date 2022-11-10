@echo off
echo Be sure to compile in Release mode
pause

echo Copying the DLL
@echo on

xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Lib\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\Release\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\Release\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\UnitTests\bin\Debug\" /D /Y

xcopy Release\ProteowizardWrapper.pdb "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Lib\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\Release\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\Release\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\UnitTests\bin\Debug\" /D /Y

xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\GordonSlysz\DeconTools_IQ\Library\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\Documents\Projects\GordonSlysz\DeconTools_IQ\Library\" /D /Y
xcopy Release\ProteowizardWrapper.xml "F:\Documents\Projects\GordonSlysz\DeconTools_IQ\Library\" /D /Y

xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\GordonSlysz\DeconTools_IQ\DeconConsole\bin\x64\Debug\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\GordonSlysz\DeconTools_IQ\DeconConsole\bin\x86\Debug\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\GordonSlysz\DeconTools_IQ\DeconConsole\bin\x86\Release\" /D /Y

xcopy Release\ProteowizardWrapper.dll "C:\DMS_Programs\MSFileInfoScanner" /D /Y

@echo off
echo.
echo You must manually copy ProteowizardWrapper.dll to
echo \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner
echo.
pause