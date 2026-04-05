@echo off
echo Be sure to compile in Release mode
pause

echo Copying the DLL
@echo on

xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Common\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Program\bin\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Shared\bin\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\Plugins\AM_NOM_Annotation_Plugin\bin\" /D /Y

xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Lib\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\Release\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\Release\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\UnitTests\bin\Debug\" /D /Y

xcopy Release\ProteowizardWrapper.pdb "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Lib\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\Release\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\Release\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\UnitTests\bin\Debug\" /D /Y

xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\Gordon_Slysz\DeconTools_IQ\Library\" /D /Y
xcopy Release\ProteowizardWrapper.pdb "F:\Documents\Projects\Gordon_Slysz\DeconTools_IQ\Library\" /D /Y
xcopy Release\ProteowizardWrapper.xml "F:\Documents\Projects\Gordon_Slysz\DeconTools_IQ\Library\" /D /Y

xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\Gordon_Slysz\DeconTools_IQ\DeconConsole\bin\x64\Debug\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\Gordon_Slysz\DeconTools_IQ\DeconConsole\bin\x86\Debug\" /D /Y
xcopy Release\ProteowizardWrapper.dll "F:\Documents\Projects\Gordon_Slysz\DeconTools_IQ\DeconConsole\bin\x86\Release\" /D /Y

xcopy Release\ProteowizardWrapper.dll "C:\DMS_Programs\MSFileInfoScanner" /D /Y

@echo off
echo.
echo You must manually copy ProteowizardWrapper.dll to
echo \\Proto-3\DMS_Programs_Dist\CaptureTaskManagerDistribution\MSFileInfoScanner
echo.
pause