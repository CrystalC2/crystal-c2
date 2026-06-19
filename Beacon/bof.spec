x86:
    push $OBJECT
        make object +optimize
        import "LoadLibraryA, GetProcAddress, BeaconDataParse, BeaconDataExtract, BeaconDataPtr, BeaconDataShort, BeaconDataInt, BeaconDataLength, BeaconFormatAlloc, BeaconFormatInt, BeaconFormatPrintf, BeaconFormatToString, BeaconFormatReset, BeaconFormatFree, BeaconPrintf, BeaconOutput"
        mergelib "libtcg.x86.zip"
        export

x64:
    push $OBJECT
        make object +optimize
        import "LoadLibraryA, GetProcAddress, BeaconDataParse, BeaconDataExtract, BeaconDataPtr, BeaconDataShort, BeaconDataInt, BeaconDataLength, BeaconFormatAlloc, BeaconFormatInt, BeaconFormatPrintf, BeaconFormatToString, BeaconFormatReset, BeaconFormatFree, BeaconPrintf, BeaconOutput"
        mergelib "libtcg.x64.zip"
        export