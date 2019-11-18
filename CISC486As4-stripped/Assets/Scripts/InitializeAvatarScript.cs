using UnityEngine;
using System.Collections;

/// <summary>
/// Each avatar has a copy of this script attached. Once the decision has been made whether this avatar
/// is acting as the local or remote avatar, this enables either the local or remote avatar script.
/// </summary>
public class InitializeAvatarScript : MonoBehaviour {

	/// <summary>
	/// Initializes this avatar as the player avatar. Activates LocalAvatarScript and Camera; deactivates remote avatar script.
	/// </summary>
	public void InitializeAsPlayer() {
		LocalAvatarScript lac = GetComponent<LocalAvatarScript>();
		lac.enabled = true;

		GameObject cam = transform.Find("Camera").gameObject;
		cam.SetActive(true);

		RemoteAvatarScript rac = GetComponent<RemoteAvatarScript>();
		rac.enabled = false;
	}

	/// <summary>
	/// Initializes this avatar script as the remote avatar. Activate RemoteAvatarScript. Deactivate camera and LocalAvatarScript.
	/// </summary>
	public void InitializeAsRemote() {
		LocalAvatarScript las = GetComponent<LocalAvatarScript>();
		las.enabled = false;

		GameObject cam = transform.Find("Camera").gameObject;
		cam.SetActive(false);

		RemoteAvatarScript ras = GetComponent<RemoteAvatarScript>();
		ras.enabled = true;
	}
}
