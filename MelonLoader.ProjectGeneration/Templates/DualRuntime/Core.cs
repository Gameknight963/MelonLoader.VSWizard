#if MONO && IL2CPP
#error Define only one runtime symbol.
#elif MONO
$MONO_CORE$
#elif IL2CPP
$IL2CPP_CORE$
#else
#error Select the Mono or Il2Cpp configuration.
#endif
