using UnityEngine;
using System.Collections;

public class AnimationInfo {

	public static readonly float walkSpeed = 6.0f;
	public static readonly float fastWalkSpeed = walkSpeed * 2;
	public static readonly float turnSpeed = 75.0f; // degrees/second

	// Triggers used to change state
	public static readonly int walkHash = Animator.StringToHash ("Walk");
	public static readonly int idleHash = Animator.StringToHash ("Idle");
	public static readonly int turnLeftHash = Animator.StringToHash ("TurnLeft");
	public static readonly int turnRightHash = Animator.StringToHash ("TurnRight");

	// Names of animation states
	public static readonly int idleStateHash = Animator.StringToHash ("idle");
	public static readonly int walkingStateHash = Animator.StringToHash ("walking");
	public static readonly int turningLeftStateHash = Animator.StringToHash ("left_turn");
	public static readonly int turningRightStateHash = Animator.StringToHash ("right_turn");

	// movement states
	public static readonly byte idle = (byte)0;
	public static readonly byte walking = (byte)1;
	public static readonly byte turningLeft = (byte)2;
	public static readonly byte walkingTurningLeft = (byte)3;
	public static readonly byte turningRight = (byte)4;
	public static readonly byte walkingTurningRight = (byte)5;

	public static bool IsLegalMovementState(byte m) {
		return m==idle || m==walking || m==turningLeft || m==walkingTurningLeft
			|| m==turningRight || m==walkingTurningRight;
	}
}

/// <summary>
/// Local avatar script. This script handles user input ("W" to move forwards, "A" and "D" to rotate.)
/// It updates the movement state (idle, walking, turningLeft, turningRight, walkingTurningLeft, or walkingTurningRight.)
/// The movement state is used to determine the correct animation.
/// </summary>
public class LocalAvatarScript : MonoBehaviour {

	protected Animator _anim;

	// Locomotion state - idle, walking or turning
	public byte movementState = AnimationInfo.idle;

	void Start () {
		_anim = gameObject.GetComponent<Animator> ();
	}

	/// <summary>
	/// Polls user input (W/A/D) and determines what the movement state should be.
	/// For example, if the W and A keys are down, movement state is walkingTurningLeft.
	/// Triggers the appropriate animation if a change in animation state is required.
	/// </summary>
	void UpdateWalkingState() {
		if(Input.GetKeyDown(KeyCode.W)) {
			// Move to walking animation
			_anim.SetTrigger(AnimationInfo.walkHash);
			movementState = AnimationInfo.walking;
		} else if(Input.GetKeyUp(KeyCode.W)) {
			// Stop walking. If still turning, then show turning animation;
			// otherwise move to idle animation
			if(movementState == AnimationInfo.walkingTurningLeft) {
				movementState = AnimationInfo.turningLeft;
				_anim.SetTrigger(AnimationInfo.turnLeftHash);
			} else if(movementState == AnimationInfo.walkingTurningRight) {
				movementState = AnimationInfo.turningRight;
				_anim.SetTrigger(AnimationInfo.turnRightHash);
			} else {
				Debug.Assert(movementState == AnimationInfo.walking);
				movementState = AnimationInfo.idle;
				_anim.SetTrigger(AnimationInfo.idleHash);
			}
		}

		if(Input.GetKeyDown(KeyCode.A)) {
			// Start turning left. If walking, stick with walking animation, otherwise show
			// turning animation.
			if(movementState == AnimationInfo.idle || movementState == AnimationInfo.turningRight) {
				movementState = AnimationInfo.turningLeft;
				_anim.SetTrigger(AnimationInfo.turnLeftHash);
			} else if(movementState == AnimationInfo.walking) {
				movementState = AnimationInfo.walkingTurningLeft;
			}
		} else if(Input.GetKeyDown(KeyCode.D)) {
			// Start turning right. If walking, stick with walking animation, otherwise show
			// turning animation.
			if(movementState == AnimationInfo.idle || movementState == AnimationInfo.turningLeft) {
				movementState = AnimationInfo.turningRight;
				_anim.SetTrigger(AnimationInfo.turnRightHash);
			} else if(movementState == AnimationInfo.walking) {
				movementState = AnimationInfo.walkingTurningRight;
			}
		}

		if(Input.GetKeyUp(KeyCode.D)) {
			// Stop turning. If walking, leave walking animation; otherwise stop turning
			// animation.
			if(movementState == AnimationInfo.turningRight) {
				movementState = AnimationInfo.idle;
				_anim.SetTrigger(AnimationInfo.idleHash);
			} else if(movementState == AnimationInfo.walkingTurningRight) {
				movementState = AnimationInfo.walking;
			}
		} else if(Input.GetKeyUp(KeyCode.A)) {
			// Stop turning. If walking, leave walking animation; otherwise stop turning
			// animation.
			if(movementState == AnimationInfo.turningLeft) {
				movementState = AnimationInfo.idle;
				_anim.SetTrigger(AnimationInfo.idleHash);
			} else if(movementState == AnimationInfo.walkingTurningLeft) {
				movementState = AnimationInfo.walking;
			}
		}
	}

	/// <summary>
	/// Moves the avatar in the direction indicated by the movement state. If the
	/// avatar is walking, translates the avatar forward. If the avatar is turning,
	/// rotates the avatar.
	/// </summary>
	void MoveAvatar() {
		if(movementState == AnimationInfo.walking
				|| movementState == AnimationInfo.walkingTurningLeft || movementState == AnimationInfo.walkingTurningRight) {
			transform.Translate(Vector3.forward * AnimationInfo.walkSpeed * Time.deltaTime);
		}

		if (movementState == AnimationInfo.turningLeft || movementState == AnimationInfo.walkingTurningLeft) {
			transform.Rotate(Vector3.up * -AnimationInfo.turnSpeed * Time.deltaTime);
		} else if(movementState == AnimationInfo.turningRight || movementState == AnimationInfo.walkingTurningRight) {
			transform.Rotate(Vector3.up * AnimationInfo.turnSpeed * Time.deltaTime);
		}
	}

	void Update () {
		UpdateWalkingState();
		MoveAvatar();
	}
}
