using UnityEngine;

namespace Buttermilch {
    [RequireComponent(typeof(Rigidbody))]
    public class HoverShip : MonoBehaviour {

        [Header("Ship handling")]
        [SerializeField] private float _fwdAccel = 50f;
        [SerializeField] private float _fwdMaxSpeed = 200f;
        private float _oldFwdMaxSpeed;
        [SerializeField] private float _maxBoost = 300f;
        [SerializeField] private float _brakeSpeed = 70f;
        [SerializeField] private float _turnSpeed = 10f;
        [SerializeField] private float _turnSpeedInAir = 3f;
        [SerializeField] private float _currentSpeed;
        private bool _isDead;
        private bool _canBoost;
        private Transform _startPos;
        [Header("Fuel")]
        [SerializeField] private float _maxFuel = 100f;
        [SerializeField] private float _fuelConsumption = 1f;
        [SerializeField] private float _currentFuel;
        [Header("Hovering")]
        [SerializeField] private LayerMask groundLayer; //objects we want to be able to hover on
        private Rigidbody _rb;
        private Vector3 previousFrameUpDir; //stores transform.up
        [SerializeField] private float _hoverHeight = 3f; //Distance from the ground
        [SerializeField] private float _heightSmoothing = 10f; //How fast the ship will readjust to "hoverHeight"
        [SerializeField] private float _normalRotSmoothing = 50f; //How fast the ship will adjust its rotation to match ground normal
        private float _smoothY = 1f;
        [SerializeField] private float _startDelay = 0.2f;
        private bool _isGrounded; //Also for modelVFX
        [Header("Dropping")]
        [SerializeField] private float _dropOffTime = 0.2f;
        private bool _isDroppingOff;
        private float _oldDropOffTime;
        [SerializeField] private float _rotationLerp = 7f; //how fast we will rotate towards the ground if ship is in the air
        [SerializeField] private float _downForce = 5f; //how fast we will sink down if ship is in the air
        private float _olddownForce;
        private bool _dropOffForceAdded;

        private void Start() {
            //ship handling
            _currentSpeed = 0f;
            _oldFwdMaxSpeed = _fwdMaxSpeed;
            _canBoost = true;
            _isDead = false;
            _startPos = transform;
            //fuel
            _currentFuel = _maxFuel;
            //hovering
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _isGrounded = true;
            //dropping
            _isDroppingOff = false;
            _oldDropOffTime = _dropOffTime;
            _olddownForce = _downForce;
            _dropOffForceAdded = false;

        }

        private void Update() {

            if (_startDelay > 0f) _startDelay -= Time.deltaTime;
            else DrivingBehaviour();

            previousFrameUpDir = transform.up;
        }

        private void FixedUpdate() {
            if (_isDead) return;

            TurningBehavior();
            HoverBehavior();
            MoveShip();
        }

        private void DrivingBehaviour() {
            if (_isDead) {
                return;
            }

            if (IsAccelerating()) {
                Accelerate();
                ReduceFuel();
                BoostingBehavior();

                if (IsToFast() || IsOutOfFuel()) Brake(_brakeSpeed);
            } else {
                Brake(_brakeSpeed);
            }

            if (IsOutOfFuel()) _canBoost = false;
            if (!IsMoving() && _isGrounded) _rb.velocity = Vector3.zero;

            if (_isDroppingOff) _dropOffTime -= Time.deltaTime;

        }

        /// <summary>
        /// Handles boosting logic.
        /// </summary>
        private void BoostingBehavior() {
            if (IsBoosting() && _canBoost) {
                _fwdMaxSpeed = _maxBoost;
                return;
            }
            _fwdMaxSpeed = _oldFwdMaxSpeed;
        }

        /// <summary>
        /// Handles either ground- or air behaviour.
        /// </summary>
        private void HoverBehavior() {
            if (!_isGrounded) {
                AirBehavior();
                return;
            }
            GroundBehavior();
        }


        /// <summary>
        /// Tries to rotate towards the ground by shooting a raycast straight down. 
        /// Uses same logic as in GroundBehaviour().
        /// Then adds a down force to the spaceship to let it sink. Eventually the raycast will then again hit something.
        /// </summary>
        private void AirBehavior() {
            RaycastHit rotationHit;

            if (Physics.Raycast(transform.position, Vector3.down * 50, out rotationHit, groundLayer)) {
                Vector3 desiredUp = Vector3.Lerp(transform.up, Vector3.up, Time.deltaTime * _normalRotSmoothing);
                Quaternion tilt = Quaternion.FromToRotation(transform.up, desiredUp) * transform.rotation;
                transform.localRotation = Quaternion.Lerp(transform.localRotation, tilt, _rotationLerp * Time.deltaTime);
            }

            if (_dropOffTime > 0f) return;

            _downForce += 10 * Time.deltaTime;
            transform.localPosition += Vector3.down * _downForce * Time.deltaTime;
            RaycastHit hit;

            if (!Physics.Raycast(transform.position, Vector3.down * 50, out hit, groundLayer)) return;

            if (hit.distance <= _hoverHeight) {
                _dropOffTime = _oldDropOffTime;
                _dropOffForceAdded = false;
                _downForce = _olddownForce;
                DropOn();
            }


        }


        /// <summary>
        /// Aligns spaceship rotation to the normal of the mesh below and adjusts its height to the surface.
        /// </summary>
        private void GroundBehavior() {
            RaycastHit hit;
            if (!Physics.Raycast(transform.position, -previousFrameUpDir, out hit, 10f, groundLayer)) {
                _isGrounded = false;
                return;
            }
            _isGrounded = true;

            Vector3 desiredUp = Vector3.Lerp(previousFrameUpDir, hit.normal, Time.deltaTime * _normalRotSmoothing);
            Quaternion tilt = Quaternion.FromToRotation(transform.up, desiredUp) * transform.rotation;
            transform.localRotation = tilt;

            _smoothY = Mathf.Lerp(_smoothY, _hoverHeight - hit.distance, Time.deltaTime * _heightSmoothing);
            transform.localPosition += previousFrameUpDir * _smoothY;
        }

        /// <summary>
        /// Turns ship by input or stops angular rotation if no input.
        /// </summary>
        private void TurningBehavior() {
            if (IsTurning()) {
                TurnShip();
                return;
            }
            StopAngularRotation();
        }

        /// <summary>
        /// Turns spaceship either by ground factor or in air factor.
        /// </summary>
        private void TurnShip() {
            if (IsOutOfFuel()) return;
            float speed = _isGrounded ? _turnSpeed : _turnSpeedInAir;
                print("turning " + previousFrameUpDir * speed * Input.GetAxis("Horizontal") * Time.fixedDeltaTime);
            _rb.AddTorque(previousFrameUpDir * speed * Input.GetAxis("Horizontal") * Time.fixedDeltaTime, ForceMode.Impulse);
        }

        /// <summary>
        /// Linearly stops angular rotation over time
        /// </summary>
        private void StopAngularRotation() {
            _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, Vector3.zero, 0.05f * Time.fixedDeltaTime);
        }

        /// <summary>
        /// Moves rigidobdy by forward vector and current speed.
        /// </summary>
        /// <param name="speed"></param>
        private void MoveShip() {
            _rb.velocity = transform.forward * _currentSpeed * Time.fixedDeltaTime;
        }

        /// <summary>
        /// Reduces current speed to slow down the spaceship.
        /// </summary>
        /// <param name="brakeSpeed"></param>
        private void Brake(float brakeSpeed) {
            if (_currentSpeed > 0) {
                _currentSpeed -= brakeSpeed * Time.deltaTime;
                return;
            }
            _currentSpeed = 0f;
        }

        /// <summary>
        /// Accelerates spaceship by increasing current speed. Accelerates faster when boosting.
        /// </summary>
        private void Accelerate() {
            if (IsOutOfFuel()) return;
            float speed = IsBoosting() ? _fwdAccel * 2 : _fwdAccel;
            _currentSpeed += (_currentSpeed >= _fwdMaxSpeed) ? 0f : speed * Time.deltaTime;
        }

        /// <summary>
        /// Reduces fuel. Reduces faster when boosting.
        /// </summary>
        private void ReduceFuel() {
            _currentFuel -= IsBoosting() ? _fuelConsumption * 2 * Time.deltaTime : _fuelConsumption * Time.deltaTime;
        }

        /// <summary>
        /// Stops ship movement and disables this script.
        /// </summary>
        private void Die() {
            _isDead = true;
            _currentSpeed = 0;
            _rb.velocity = Vector3.zero;
            GetComponent<HoverShip>().enabled = false;
            transform.gameObject.isStatic = true;
        }

        private void DropOff() {
            if (!_isDroppingOff) {
                _isGrounded = false;
                _isDroppingOff = true;
            }
        }

        private void DropOn() {
            _isGrounded = true;
            _isDroppingOff = false;
        }


        /// <summary>
        /// Checks if any input is pressed for accelerating.
        /// </summary>
        /// <returns></returns>
        private bool IsAccelerating() => (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetAxis("Right Trigger") > 0);

        /// <summary>
        /// Checks if any input is pressed for boosting.
        /// </summary>
        /// <returns></returns>
        private bool IsBoosting() => ((Input.GetKey(KeyCode.Space) || Input.GetButton("A Button")) && _canBoost);

        /// <summary>
        /// Checks if any input is pressed for turning.
        /// </summary>
        /// <returns></returns>
        private bool IsTurning() => (Input.GetAxis("Horizontal") > 0 || Input.GetAxis("Horizontal") < 0);

        private bool IsMoving() => _currentSpeed > 0f;
        private bool IsOutOfFuel() => _currentFuel <= 0f;
        private bool IsToFast() => (_currentSpeed >= _fwdMaxSpeed);



    }
}
