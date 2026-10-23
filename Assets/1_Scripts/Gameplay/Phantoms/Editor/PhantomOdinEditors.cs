using UnityEditor;
using Sirenix.OdinInspector.Editor;
using Gameplay.Phantoms;
using Player.Mechanics;

namespace Gameplay.Phantoms.Editor
{
    /// <summary>
    /// Description: Custom Odin Editors for NetworkBehaviour components.
    /// Context: Unity Editor only.
    /// Justification: Mirror declares a global [CustomEditor(typeof(NetworkBehaviour), true)] which forces Unity
    /// to use standard UnityEditor.Editor and suppresses Odin Inspector on all NetworkBehaviours by default.
    /// Declaring specific CustomEditors inheriting from OdinEditor overrides Mirror's fallback, restoring full Odin
    /// drawing (Buttons, Titles, ReadOnly, ShowInInspector, GUIColors) on these components.
    /// </summary>
    [CustomEditor(typeof(PhantomRoomSpawner), true)]
    [CanEditMultipleObjects]
    public class PhantomRoomSpawnerEditor : OdinEditor {}

    [CustomEditor(typeof(PhantomBase), true)]
    [CanEditMultipleObjects]
    public class PhantomBaseEditor : OdinEditor {}

    [CustomEditor(typeof(PhantomFlyingWisp), true)]
    [CanEditMultipleObjects]
    public class PhantomFlyingWispEditor : OdinEditor {}

    [CustomEditor(typeof(PhantomStandard), true)]
    [CanEditMultipleObjects]
    public class PhantomStandardEditor : OdinEditor {}

    [CustomEditor(typeof(PlayerHealth), true)]
    [CanEditMultipleObjects]
    public class PlayerHealthEditor : OdinEditor {}

    [CustomEditor(typeof(PlayerMouthVacuum), true)]
    [CanEditMultipleObjects]
    public class PlayerMouthVacuumEditor : OdinEditor {}
}
