using UnityEngine;
using System.Collections;

[RequireComponent(typeof(OVRSpatialAnchor))]
public class SpatialAnchorUIBinder : MonoBehaviour
{
    [Header("Placement Controller Anchor")]
    [Tooltip("Assign your real-world Right Hand controller tracking anchor here.")]
    public Transform trackingHand;

    private bool _isPlacingModeActive = true;
    private OVRSpatialAnchor _spatialAnchor;
    private string _playerPrefSaveKey = "RWS_SpatialUI_Anchor_UUID";

    void Awake()
    {
        _spatialAnchor = GetComponent<OVRSpatialAnchor>();
    }

    void Start()
    {
        // Automatically attempt to locate and snap to a previously saved layout on boot
        StartCoroutine(TryLoadExistingAnchor());
    }
    void Update()
    {
        // If the UI hasn't been permanently locked yet, make it float directly in front of your controller
        if (_isPlacingModeActive && trackingHand != null)
        {
            // Offsets the canvas slightly forward out from the hand so it remains visible
            transform.position = trackingHand.position + (trackingHand.forward * 0.4f);
            
            // Rotates the panel canvas to look comfortably back toward your eyes
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }

    /// <summary>
    /// Call this function from an inspector button event (e.g., when the user places the canvas exactly where they want it).
    /// </summary>
    public void PinCanvasToCurrentLocation()
    {
        // If an old anchor exists, instantiate a fresh tracking state context
        if (!_spatialAnchor.Created)
        {
            _isPlacingModeActive = false;
            StartCoroutine(CreateAndSaveAnchorCoroutine());
        }
        else
        {
            Debug.LogWarning("Anchor already established! Clear or restart to remap.");
        }
    }

    private IEnumerator CreateAndSaveAnchorCoroutine()
    {
        Debug.Log("Registering physical environment space layout...");
        
        // Wait until the underlying Meta Runtime architecture localizes the point safely
        while (!_spatialAnchor.Created)
        {
            yield return null;
        }

        // Save the unique tracking profile token locally on the headset device storage registry
        if (_spatialAnchor.Created)
        {
            PlayerPrefs.SetString(_playerPrefSaveKey, _spatialAnchor.Uuid.ToString());
            PlayerPrefs.Save();
            Debug.Log($"Spatial UI successfully locked! Saved UUID: {_spatialAnchor.Uuid}");
        }
    }

    private IEnumerator TryLoadExistingAnchor()
    {
        if (!PlayerPrefs.HasKey(_playerPrefSaveKey))
        {
            Debug.Log("No saved spatial layout found for the UI panel canvas. Ready for initial alignment.");
            yield break;
        }

        string savedUuidString = PlayerPrefs.GetString(_playerPrefSaveKey);
        System.Guid savedUuid = new System.Guid(savedUuidString);

        Debug.Log($"Attempting to load spatial coordinates for anchor point: {savedUuidString}");

        // Instantiate an array payload query to search the Meta boundary platform subsystem
        var uuids = new System.Guid[] { savedUuid };
        var queryOptions = new OVRSpatialAnchor.LoadOptions
        {
            Timeout = 0,
            StorageLocation = OVRSpace.StorageLocation.Local,
            Uuids = uuids
        };

        // Query the cloud or tracking space layers to match the environment mapping signatures
        OVRSpatialAnchor.LoadUnboundAnchors(queryOptions, OnAnchorsLoaded);
    }

    private void OnAnchorsLoaded(OVRSpatialAnchor.UnboundAnchor[] unboundAnchors)
    {
        if (unboundAnchors == null || unboundAnchors.Length == 0)
        {
            Debug.LogError("Failed to resolve physical anchor points. Device tracking map may have shifted.");
            return;
        }

        // Bind the unbound spatial metadata tracking back onto our active scene transform hierarchy
        unboundAnchors[0].BindTo(_spatialAnchor);
        Debug.Log("UI Canvas successfully localized and snapped to its physical room boundary position!");
    }

    /// <summary>
    /// Clears out the anchor history so you can manually realign your dashboard configuration panel.
    /// </summary>
    public void EraseAnchorHistory()
    {
        if (PlayerPrefs.HasKey(_playerPrefSaveKey))
        {
            PlayerPrefs.DeleteKey(_playerPrefSaveKey);
            Debug.Log("Spatial UI profile erased. Restart scene to choose a new anchor point.");
        }
    }
}