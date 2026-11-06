using System.Runtime.CompilerServices;

// Lets the EditMode tests (Assets/**/Editor, compiled into Assembly-CSharp-Editor) exercise internal
// rules directly - e.g. how a networked shot bounces or how the server checks a weapon's cadence -
// without widening those members to the rest of the game.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
