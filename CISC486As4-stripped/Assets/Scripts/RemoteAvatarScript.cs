using UnityEngine;
using System.Collections;

/// <summary>
///  Script attached to the remote avatar. The remote avatar has a target position which
/// may be updated when avatar movement messages arrive from the server. The script moves
/// the avatar toward the target position.
/// </summary>
public class RemoteAvatarScript : MonoBehaviour {

	// Only update position/rotation if it is bigger than the threshold value
	const float _positionUpdateThreshold = 0.1f;
	const float _rotationUpdateThreshold = 1;

	const float correctionThreshold = 1f;

	// Where the avatar should be
	Vector3 _targetPosition;
	bool _hasTargetPosition = false;

	// Which way the avatar should be pointing
	Quaternion _targetRotation;
	bool _hasTargetRotation = false;

	// Which movement state the avatar should be in
	byte _targetMovementState;
	bool _hasTargetMovementState = false;

	/// <summary>
	/// When remote messages arrive specifying where this avatar should be, the target
	/// position is updated. This script will then move the avatar toward this target position.
	/// </summary>
	/// <value>The target position.</value>
	public Vector3 targetPosition {
		get { return _targetPosition; }
		set {
			_targetPosition = value;
			_hasTargetPosition = true;
		}
	}

	/// <summary>
	/// When remote messages arrive specifying where this avatar should be, the target
	/// rotation is updated. This script will then turn the avatar toward this target position.
	/// </summary>
	/// <value>The target rotation.</value>
	public Quaternion targetRotation {
		get{ return _targetRotation; }
		set {
			_targetRotation = value;
			_hasTargetRotation = true;
		}
	}

	/// <summary>
	/// When remote messages arrive specifying what animation this avatar should be performing, the target
	/// movement state is updated. This script will then initiate this animation.
	/// </summary>
	/// <value>The state of the target movement.</value>
	public byte targetMovementState {
		get{ return _targetMovementState; }
		set{
			_targetMovementState = value;
			_hasTargetMovementState = true;
		}
	}

	/// <summary>
	/// This remote avatar will be moved using a choice of no prediction, dead reckoning, or
	/// dead reckoning with smooth corrections.
	/// </summary>
	public enum Algorithm { None, DeadReckoning, SmoothCorrections };
	Algorithm _updateAlgorithm = Algorithm.None;

	public Algorithm UpdateAlgorithm {
		set  {
			_updateAlgorithm = value;
		}
		get {
			return _updateAlgorithm;
		}
	}

	Animator _anim;
	byte _movementState;



	/// <summary>
	/// Update the avatar's position, taking into account the target position and rotation,
	/// and applying the currently selected update algorithm.
	/// </summary>
	void UpdateAvatarPosition() {
		if(UpdateAlgorithm == Algorithm.None) {
			Debug.Log("Updating avatar position with algorithm 'none'");
			UpdateAvatarPositionImmediately();
		} else if(UpdateAlgorithm == Algorithm.DeadReckoning) {
			Debug.Log("Updating avatar position with algorithm 'dead reckoning'");
			MoveAvatarWithDeadReckoning(); 
		} else {
			Debug.Assert(UpdateAlgorithm == Algorithm.SmoothCorrections);
			Debug.Log("Updating avatar position with algorithm 'smooth corrections'");
			SmoothlyCorrectAvatarPosition();
		}
	}


	/// <summary>
	/// Update the avatar's position taking into account the target position and rotation,
	/// </summary>
	void UpdateAvatarPositionImmediately(){
		if(_hasTargetPosition)
		transform.position = targetPosition;
		if(_hasTargetRotation)
		transform.rotation = targetRotation;
	}

	/// <summary>
	/// Update the avatar's position using the dead reckoning algorithm
	/// </summary>
	void MoveAvatarWithDeadReckoning(){

		// Find current velocity
		Vector3 velocity = transform.forward * AnimationInfo.walkSpeed;

		// If in moving state, use dead reckoning
		if(targetMovementState == AnimationInfo.walking || 
			targetMovementState == AnimationInfo.walkingTurningLeft ||
			targetMovementState == AnimationInfo.walkingTurningLeft){
				transform.position = transform.position + velocity * Time.deltaTime;
		}
		// else, snap to correct position
		else{
			transform.position = targetPosition;
		}

		// smoothly update rotation
		transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, AnimationInfo.turnSpeed * Time.deltaTime);
	}

	/// <summary>
	/// When positional updates for the remote avatar arrive, 
	/// if the correction is large, use smooth corrections to fix it. 
	/// The correction is considered large if the difference in position 
	/// between the new position and the currently saved position is above some threshold. 
	/// When correcting, the remote avatar should be set to move at double its usual speed.
	/// </summary>
	void SmoothlyCorrectAvatarPosition(){

		// If in moving state, use dead reckoning
		if(targetMovementState == AnimationInfo.walking || 
			targetMovementState == AnimationInfo.walkingTurningLeft ||
			targetMovementState == AnimationInfo.walkingTurningLeft){

			// Apply dead reckoning
			Vector3 velocity = transform.forward * AnimationInfo.walkSpeed;
			transform.position = transform.position + velocity * Time.deltaTime;
		} else{
			if((targetPosition - transform.position).magnitude > correctionThreshold){
				applySmoothing();
			} else{
				transform.position = targetPosition;
			}
		}

		void applySmoothing(){
			// rotate towards target
			transform.LookAt(targetPosition);
			// Move to target @ 2x speed
			Vector3 velocity = (targetPosition - transform.position).normalized * AnimationInfo.fastWalkSpeed;
			transform.position = transform.position + velocity * Time.deltaTime;
		}
		
		// smoothly update rotation
		transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, AnimationInfo.turnSpeed * Time.deltaTime);
	}

	/// <summary>
	/// Updates the animation used by the remote avatar. If the avatar has moved in
	/// the last frame, plays the walking animation. If the avatar has not moved but has
	/// rotated, plays the turning animation. If the avatar has not moved or rotated, plays
	/// the idle animation.
	/// </summary>
	void UpdateAnimation() {
		if(_hasTargetMovementState && targetMovementState != _movementState) {
			// move into walking state from idle or turning
			if(targetMovementState == AnimationInfo.walking
				&& (_movementState == AnimationInfo.idle || _movementState == AnimationInfo.turningLeft
					|| _movementState == AnimationInfo.turningRight)) {
				_anim.SetTrigger(AnimationInfo.walkHash);
			}

			// move into idle state
			if(targetMovementState == AnimationInfo.idle) {
				_anim.SetTrigger(AnimationInfo.idleHash);
			}

			// move into turning left state
			if(targetMovementState == AnimationInfo.turningLeft) {
				_anim.SetTrigger(AnimationInfo.turnLeftHash);
			}

			// move into turning right state
			if(targetMovementState == AnimationInfo.turningRight) {
				_anim.SetTrigger(AnimationInfo.turnRightHash);
			}

			// move into walking-turning-left state
			if(targetMovementState == AnimationInfo.walkingTurningLeft
				&& _movementState != AnimationInfo.walking
				&& _movementState != AnimationInfo.walkingTurningRight) {
				_anim.SetTrigger(AnimationInfo.walkHash);
			}

			// move into walking-turning-right state
			if(targetMovementState == AnimationInfo.walkingTurningRight
					&& _movementState != AnimationInfo.walking
					&& _movementState != AnimationInfo.walkingTurningLeft) {
				_anim.SetTrigger(AnimationInfo.walkHash);
			}

			_movementState = targetMovementState;
			_hasTargetMovementState = false;
		}
	}


	void Update () {

		// Play the correct animation for the remote avatar
		UpdateAnimation();

		// Move and rotate the remote avatar
		UpdateAvatarPosition();
	}


	void Start () {
		_anim = gameObject.GetComponent<Animator> ();
	}
}
