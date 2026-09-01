# Vario

Vario is the accessible companion application installed with eSpeak. It edits
the eSpeak voice tokens in the registry view matching the installed SAPI
edition, lets the user combine languages with variants, and controls the
optional Sonic speed boost. Registry changes require administrator rights.

Available and installed voices are presented as checkable trees. Checking a
language selects all of its voices, while expanding it permits individual
selection. Delete removes the focused installed voice after confirmation;
Shift+Delete removes it without confirmation. The Remove selected button always
asks for confirmation.

Each installed voice has its own modulation value (`espeakRANGE`) from 0 to
100. Zero is monotone and 50 is eSpeak's normal value. This is independent of
the base pitch setting.

The interface uses Polish when Windows uses a Polish UI culture and English
otherwise. SAPI applications may need to be restarted after applying changes.
