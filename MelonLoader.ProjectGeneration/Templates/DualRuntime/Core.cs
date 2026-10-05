#if MONO && IL2CPP
#error Define only one runtime symbol.
#elif MONO
$MONO_CORE$
#elif IL2CPP
$IL2CPP_CORE$
#else
#error Select a Mono or Il2Cpp build configuration.
#endif
