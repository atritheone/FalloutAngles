using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)] // ensure controls exist before other scripts Awake/OnEnable
public class PlayerControls : MonoBehaviour
{
    public InputSystemActions Controls { get; private set; }
    private InputSystemActions.PlayerActions playerActions;
    private System.Action<InputAction.CallbackContext> onPipBoyPerformed;

    private void Awake()
    {
        EnsureControlsInitialized();
        onPipBoyPerformed = OnPipBoyPerformed;
    }

    private void OnEnable()
    {
        EnsureControlsInitialized();
        playerActions.PipBoy.performed += onPipBoyPerformed;

        playerActions.Enable();
    }

    private void OnDisable()
    {
        if (Controls == null) return;

        playerActions.PipBoy.performed -= onPipBoyPerformed;
        playerActions.Disable();
    }

    private void OnDestroy()
    {
        Controls?.Dispose();
        Controls = null;
        playerActions = default;
    }

    private void EnsureControlsInitialized()
    {
        if (Controls != null)
            return;

        Controls = new InputSystemActions();
        playerActions = Controls.Player;
    }

    private void OnPipBoyPerformed(InputAction.CallbackContext _)
    {
        if (UI.ConsoleController.IsOpen)
            return;

        PipBoyController controller = FindFirstPipBoyControllerIncludingInactive();
        if (!controller)
            return;

        // Active PipBoy controllers handle their own input subscription.
        if (controller.gameObject.activeInHierarchy)
            return;

        controller.TogglePipBoy();
    }

    private static PipBoyController FindFirstPipBoyControllerIncludingInactive()
    {
        PipBoyController[] controllers = Resources.FindObjectsOfTypeAll<PipBoyController>();
        PipBoyController firstActiveInScene = null;

        for (int i = 0; i < controllers.Length; i++)
        {
            PipBoyController controller = controllers[i];
            if (!controller)
                continue;

            GameObject controllerObject = controller.gameObject;
            if (!controllerObject || !controllerObject.scene.IsValid())
                continue;

            if (!controllerObject.activeInHierarchy)
                return controller;

            if (!firstActiveInScene)
                firstActiveInScene = controller;
        }

        return firstActiveInScene;
    }
}
