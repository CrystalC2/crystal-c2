typedef struct {
    char * original;
    char * buffer;
    int    length;
    int    size;
} datap, formatp;

/**
 * Data Parser API
 */

DECLSPEC_IMPORT void   BeaconDataParse   ( datap * parser, char * buffer, int size );
DECLSPEC_IMPORT char * BeaconDataExtract ( datap * parser, int * size );
DECLSPEC_IMPORT char * BeaconDataPtr     ( datap * parser, int size );
DECLSPEC_IMPORT short  BeaconDataShort   ( datap * parser );
DECLSPEC_IMPORT int    BeaconDataInt     ( datap * parser );
DECLSPEC_IMPORT int    BeaconDataLength  ( datap * parser );

/**
 * Format API
 */

DECLSPEC_IMPORT void   BeaconFormatAlloc    ( formatp * parser, int maxsz );
DECLSPEC_IMPORT void   BeaconFormatAppend   ( formatp * parser, char * data, int len );
DECLSPEC_IMPORT void   BeaconFormatInt      ( formatp * parser, int val );
DECLSPEC_IMPORT void   BeaconFormatPrintf   ( formatp * parser, char * fmt, ... );
DECLSPEC_IMPORT char * BeaconFormatToString ( formatp * parser, int * size );
DECLSPEC_IMPORT void   BeaconFormatReset    ( formatp * parser );
DECLSPEC_IMPORT void   BeaconFormatFree     ( formatp * parser );

/**
 * Output API
 */

#define CALLBACK_OUTPUT      0x0
#define CALLBACK_METADATA    0x1
#define CALLBACK_OUTPUT_OEM  0x1e
#define CALLBACK_OUTPUT_UTF8 0x20
#define CALLBACK_ERROR       0x0d
#define CALLBACK_CUSTOM      0x1000
#define CALLBACK_CUSTOM_LAST 0x13ff

DECLSPEC_IMPORT void BeaconPrintf   ( int type, char * fmt, ... );
DECLSPEC_IMPORT void BeaconOutput   ( int type, char * data, int len );