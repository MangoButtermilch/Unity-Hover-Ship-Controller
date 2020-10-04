using UnityEngine;

namespace Buttermilch {
    [RequireComponent (typeof (Rigidbody))]
    public class HoverShip : MonoBehaviour {

        [Header ("Ship handling")]
        [SerializeField] private float _fwdAccel = 50f; //accelaterion
        [SerializeField] private float _fwdMaxSpeed = 200f; //max speed without boosting
        private float _oldFwdMaxSpeed;
        [SerializeField] private float _maxBoost = 300f; //max speed with boosting
        [SerializeField] private float _brakeSpeed = 70f;
        [SerializeField] private float _turnSpeed = 10f;
        [SerializeField] private float _turnSpeedInAir = 3f;
        private float _input = 0f; //horizontal axis
        [SerializeField] private float _currentSpeed;
        private bool _isDead;
        private bool _canBoost;
        private Transform _startPos;
        [Header ("Fuel")]
        [SerializeField] private float _maxFuel = 100f;
        [SerializeField] private float _fuelConsumption = 1f;
        [SerializeField] private float _currentFuel;
        [Header ("Hovering")]
        [SerializeField] private LayerMask groundLayer; //objects we want to be able to hover on
        private Rigidbody _rb;
        private Vector3 previousUpDir; //stores transform.up
        [SerializeField] private float _hoverHeight = 3f; //Distance from the ground
        [SerializeField] private float _heightSmoothing = 10f; //How fast the ship will readjust to "hoverHeight"
        [SerializeField] private float _normalRotSmoothing = 50f; //How fast the ship will adjust its rotation to match ground normal
        private float _smoothY = 1f;
        [SerializeField] private float _startDelay = 0.2f;
        private bool _isGrounded; //Also for modelVFX
        [Header ("Dropping")]
        [SerializeField] private float _dropOffTime = 0.2f;
        private bool _isDroppingOff;
        private float _oldDropOffTime;
        [SerializeField] private float _rotationLerp = 7f; //how fast we will rotate towards the ground if ship is in the air
        [SerializeField] private float _positionLerp = 5f; //how fast we will sink down if ship is in the air
        private float _oldPositionLerp;
        private bool _dropOffForceAdded;

        private void Start () {
            //ship handling
            _currentSpeed = 0f;
            _oldFwdMaxSpeed = _fwdMaxSpeed;
            _canBoost = true;
            _isDead = false;
            _startPos = transform;
            //fuel
            _currentFuel = _maxFuel;
            //hovering
            _rb = GetComponent<Rigidbody> ();
            _rb.useGravity = false;
            _isGrounded = true;
            //dropping
            _isDroppingOff = false;
            _oldDropOffTime = _dropOffTime;
            _oldPositionLerp = _positionLerp;
            _dropOffForceAdded = false;

        }

        private void Update () {
            if (Input.GetButtonDown ("X Button")) {
                DropOff ();
            }
            if (_startDelay >= 0f)
                _startDelay -= Time.deltaTime;

            _input = Input.GetAxis ("Horizontal");

            if (_startDelay <= 0)
                DrivingBehaviour ();

        }

        private void DrivingBehaviour () {
            if (_isDead) {
                //Stop ship movement if dead
                _currentSpeed = 0;
                _rb.velocity = Vector3.zero;
                GetComponent<HoverShip> ().enabled = false;
                //GetComponentInChildren<MeshRenderer>().enabled = false;
                transform.gameObject.isStatic = true;
                return;
            }
            if (!_isDroppingOff) {
                //can boost while on ground
                _canBoost = true;
                //user inputs
                if (HasInput ()) {
                    //moving
                    if (!IsBoosting ()) {
                        _fwdMaxSpeed = _oldFwdMaxSpeed;
                        Accelerate (_fwdAccel);
                    } else {
                        _fwdMaxSpeed = _maxBoost;
                        Accelerate (_fwdAccel * 2);
                    }
                    //brake if we over shoot our max speed
                    if (_currentSpeed >= _fwdMaxSpeed) {
                        Brake (_brakeSpeed);
                    }
                } else {
                    //no input, so brake
                    Brake (_brakeSpeed);
                }
                //set velocity to zero if not moving and if not dropping of
                if (_currentSpeed <= 0f && _isGrounded) {
                    _rb.velocity = Vector3.zero;
                }
                //get current up dir for fixedUpdate calculations
                previousUpDir = transform.up;
            } else {
                //if dropping off stop boosting
                _canBoost = false;

                //add little force to velocity once we take off
                if (!_dropOffForceAdded) {
                    _rb.velocity += new Vector3 (0, 250, 0);
                    _dropOffForceAdded = true;
                }
                //start brake, so we can't lfy in the air
                //also decrease _brakespeed a little amount so we can glide a little longer
                Brake (_brakeSpeed / 1.5f);

                //measure time we are in the air
                _dropOffTime -= Time.deltaTime;
            }

        }

        private void FixedUpdate () {
            if (_isDead) return;

            //turning behaviour
            if (IsTurning ()) {
                if (_isDroppingOff) {
                    TurnShip (_turnSpeedInAir);
                } else {
                    TurnShip (_turnSpeed);
                }
            } else {
                //if we don't turn, then stop the angular rotation
                _rb.angularVelocity = Vector3.Lerp (_rb.angularVelocity, Vector3.zero, 0.05f * Time.fixedDeltaTime);
            }
            //if we are not in the air
            if (!_isDroppingOff) {
                //Normal alignment
                RaycastHit hit;
                if (Physics.Raycast (transform.position, -previousUpDir, out hit, 10f, groundLayer)) {
                    //we hit the ground
                    _isGrounded = true;
                    //smooth new up vector
                    Vector3 desiredUp = Vector3.Lerp (previousUpDir, hit.normal, Time.deltaTime * _normalRotSmoothing);
                    //get the angle
                    Quaternion tilt = Quaternion.FromToRotation (transform.up, desiredUp) * transform.rotation;
                    //apply rotation
                    transform.localRotation = tilt;
                    //Smoothly adjust height
                    _smoothY = Mathf.Lerp (_smoothY, _hoverHeight - hit.distance, Time.deltaTime * _heightSmoothing);

                    _smoothY = _smoothY <= 0.01f ? 0f : _smoothY;
                    transform.localPosition += previousUpDir * _smoothY;
                } else {
                    //if we don't hit anything, that means we are in the air
                    DropOff ();
                }
            } else {
                //if we are in the air
                RaycastHit rotationHit;
                //rotate towards ground normal
                if (Physics.Raycast (transform.position, Vector3.down * 50, out rotationHit, groundLayer)) {
                    Vector3 desiredUp = Vector3.Lerp (transform.up, Vector3.up, Time.deltaTime * _normalRotSmoothing);
                    //get the angle
                    Quaternion tilt = Quaternion.FromToRotation (transform.up, desiredUp) * transform.rotation;
                    //apply rotation
                    transform.localRotation = Quaternion.Lerp (transform.localRotation, tilt, _rotationLerp * Time.deltaTime);
                }

                //only sink for a given amount of time so we don't get extremly high values
                if (_dropOffTime <= 0) {
                    //increase lerp value over time, so we sink faster and faster
                    _positionLerp += 10 * Time.deltaTime;
                    //start sinking
                    transform.localPosition += Vector3.down * 9.81f * _positionLerp * Time.deltaTime;
                    RaycastHit hit;
                    //check distance to ground
                    if (Physics.Raycast (transform.position, Vector3.down * 50, out hit, groundLayer)) {
                        //if we are at our hoverheight again
                        if (hit.distance <= _hoverHeight) {
                            _dropOffTime = _oldDropOffTime;
                            //reset bool for next drop off
                            _dropOffForceAdded = false;
                            _positionLerp = _oldPositionLerp;
                            //get back on the ground
                            DropOn ();
                        }
                    }
                }
            }

            //now actually move the ship
            MoveShip (_currentSpeed * 80);

        }

        private bool IsTurning () {
            return (Input.GetAxis ("Horizontal") > 0 || Input.GetAxis ("Horizontal") < 0);
        }

        private void TurnShip (float speed) {
            _rb.AddTorque (previousUpDir * speed * _input * Time.fixedDeltaTime, ForceMode.Impulse);
        }

        private bool IsMoving () {
            return (_currentSpeed > 0);
        }

        private void MoveShip (float speed) {
            //move ship forward with current speed
            _rb.velocity = transform.forward * speed * Time.fixedDeltaTime;

        }

        private void Brake (float brakeSpeed) {
            if (_currentSpeed > 0) {
                //decrease only if it's greater than 0, so we don't start flying backwards
                _currentSpeed -= brakeSpeed * Time.deltaTime;
            } else {
                //set speed to 0 if we can't brake any more
                _currentSpeed = 0f;
            }
        }

        private void Accelerate (float speed) {
            //stop acceleration if we are out of fuel
            if (_currentFuel <= 0f) {
                _currentSpeed = Mathf.Lerp (_currentSpeed, 0f, 1f * Time.deltaTime);
                return;
            }
             //if current speed is greater than max speed, only add 0f so we don't get faster and faster
            //else apply our acceleration
            _currentSpeed += (_currentSpeed >= _fwdMaxSpeed) ? 0f : speed * Time.deltaTime;
            //reduce fuel
            _currentFuel -= IsBoosting () ? _fuelConsumption * 2 * Time.deltaTime : _fuelConsumption * Time.deltaTime;
        }

        private bool HasInput () {
            return (Input.GetKey (KeyCode.W) || Input.GetKey (KeyCode.UpArrow) || Input.GetAxis ("Right Trigger") > 0);
        }

        private bool IsBoosting () {
            //only boost if we get correct inputs and if we can boost
            return ((Input.GetKey (KeyCode.Space) || Input.GetButton ("A Button")) && _canBoost);
        }

        private void Die () {
            _isDead = true;
        }

        private void DropOff () {
            if (!_isDroppingOff) {
                //not on ground any more
                _isGrounded = false;
                _isDroppingOff = true;
            }
        }

        private void DropOn () {
            //again on ground
            _isGrounded = true;
            _isDroppingOff = false;
        }

    }
}
