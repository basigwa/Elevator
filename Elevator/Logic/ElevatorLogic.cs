using Elevator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Elevator.Logic
{
    public class ElevatorLogic
    {
        public enum State
        {
            Idle,
            Moving,
            VisitingFloor
        }

        public enum Direction
        {
            None,
            Up,
            Down
        }

        public delegate void ElevatorEventHandler(ElevatorLogic elev, State state, int floor, Direction dir);
        public event ElevatorEventHandler OnElevatorEvent;

        const int c_moveTimePerFloorSecs = 3;       // seconds
        const int c_doorRemainsOpenSecs = 3;        // seconds
        const int c_doorRemainsOpenMaxSecs = 60;        // seconds
        const int c_doorPollIntervalMsecs = 100;        // milliseconds

        char _id;
        int _numFloors;
        State _state;
        Direction _direction;
        int _currFloor;
        bool[] _floorsUp;          // Which floors to visit going up
        bool[] _floorsDown;        // Which floors to visit going down
        bool _openDoorForce;   // The door is forced to remain open when this flag is true
        bool _closeDoorForce;  // The door closes immediately if this flag is true
        List<Person> _Persons;

        bool _running;
        AutoResetEvent _signal;
        object _lockObj;

        public ElevatorLogic(char id, int numFloors)
        {
            _id = id;
            _numFloors = numFloors;
            _state = State.Idle;
            _direction = Direction.None;
            _currFloor = 1;                        // Ground floor
            _floorsUp = new bool[numFloors];
            _floorsDown = new bool[numFloors];
            _openDoorForce = false;
            _closeDoorForce = false;
            _Persons = new List<Person>();

            _signal = new AutoResetEvent(false);
            _lockObj = new object();

            for (int i = 0; i < _numFloors; i++)
            {
                _floorsUp[i] = false;
                _floorsDown[i] = false;
            }
        }

        public void GetElevatorState(out State state, out Direction dir, out int currFloor)
        {
            lock (_lockObj)
            {
                state = _state;
                dir = _direction;
                currFloor = _currFloor;
            }
        }

        // Main thread loop
        public void Run()
        {
            _running = true;

            while (_signal.WaitOne() && _running)     // Wait until we are signaled.
            {
                lock (_lockObj)
                {
                    _state = State.Moving;
                    _direction = Direction.None;
                }

                if (FindClosestMarkedFloor(out int closestMarkedFloor))     // Find closest marked floor.
                {
                    lock (_lockObj)
                    {
                        // Pick initial direction, if none is currently set.
                        if (_direction == Direction.None)
                            _direction = (closestMarkedFloor >= _currFloor) ? Direction.Up : Direction.Down;
                    }

                    do
                    {
                        DoFloor();              // Process floor
                    }
                    while (MoveElevator());     // Move, optionally reversing direction if necessary
                }

                lock (_lockObj)
                {
                    _state = State.Idle;
                    _direction = Direction.None;
                    Console.WriteLine("{0:mm:ss} - {1}.{2}: idle", DateTime.Now, _id, _currFloor);
                }
            }
        }

        public void StopElevator()
        {
            _running = false;
            _signal.Set();
        }

        public void RequestFloor(int floor, Direction dir)
        {
            lock (_lockObj)
            {
                if (dir == Direction.Up)
                    _floorsUp[floor] = true;
                else if (dir == Direction.Down)
                    _floorsDown[floor] = true;

                Console.WriteLine("{0:mm:ss} - {1}.{2}: received request on floor {3} to go {4}.", DateTime.Now, _id, _currFloor, floor, dir);
            }

            _signal.Set();
        }

        public void LoadPerson(Person Person)
        {

            // Mark the floor that the Person wants.
            lock (_lockObj)
            {
                _Persons.Add(Person);
                if (Person._destFloor >= _currFloor)
                    _floorsUp[Person._destFloor] = true;
                else
                    _floorsDown[Person._destFloor] = true;
                Console.WriteLine("{0:mm:ss} - {1}.{2}: {3} entered, chose floor {4} total {5} people", DateTime.Now, _id, _currFloor, Person._name, Person._destFloor, _Persons.Count);
            }

            _signal.Set();
        }

        public List<Person> UnloadPersons()
        {
            List<Person> unloadedPersons = new List<Person>();

            lock (_lockObj)
            {
                for (int i = _Persons.Count - 1; i >= 0; i--)
                {
                    if (_Persons[i]._destFloor == _currFloor)
                    {
                        Console.WriteLine("{0:mm:ss} - {1}.{2}: {3} left, remaining {4} people", DateTime.Now, _id, _currFloor, _Persons[i]._name, _Persons.Count-1);
                        unloadedPersons.Add(_Persons[i]);
                        _Persons.RemoveAt(i);
                    }
                }
            }

            return unloadedPersons;
        }

  

        void DoFloor()
        {
            Console.WriteLine("{0:mm:ss} - {1}.{2}: lift going {3}", DateTime.Now, _id, _currFloor, _direction);

            bool floorMarked = false;
            lock (_lockObj)            // See if this floor is marked, then clear the flag
            {
                floorMarked = (_direction == Direction.Up) ? _floorsUp[_currFloor] : _floorsDown[_currFloor];

                if (_direction == Direction.Up)
                    _floorsUp[_currFloor] = false;
                else
                    _floorsDown[_currFloor] = false;
            }

            if (floorMarked)            // If floor is marked, visit it
            {
                lock (_lockObj)
                    _state = State.VisitingFloor;

                OpenDoor();
                OnElevatorEvent(this, _state, _currFloor, _direction);   // This will cause Persons to load/unload
                CloseDoor();

                lock (_lockObj)
                    _state = State.Moving;
            }
        }

        void OpenDoor()
        {
            Console.WriteLine("{0:mm:ss} - {1}.{2}: open door", DateTime.Now, _id, _currFloor);

            // Leave door open for c_doorRemainsOpenSecs unless the force-close button is pressed.
            DateTime started = DateTime.Now;
            while (!_closeDoorForce && DateTime.Now.Subtract(started).TotalSeconds < c_doorRemainsOpenSecs)
                Thread.Sleep(c_doorPollIntervalMsecs);
        }

        void CloseDoor()
        {
            // Allow force-open button to keep the door open, but only for a maximum of c_doorRemainsOpenMaxSecs.
            DateTime started = DateTime.Now;
            while (_openDoorForce && DateTime.Now.Subtract(started).TotalSeconds < c_doorRemainsOpenMaxSecs)
                Thread.Sleep(c_doorPollIntervalMsecs);

            if (_openDoorForce)
                Console.WriteLine("{0:mm:ss} - {1}.{2}: ALARM! Force door timer exceeded!", DateTime.Now, _id, _currFloor);

            Console.WriteLine("{0:mm:ss} - {1}.{2}: close door", DateTime.Now, _id, _currFloor);
        }

        bool MoveElevator()
        {
            if (AtTopOrBottom())    // Flip direction if at top or bottom
            {
                lock (_lockObj)
                {
                    _direction = (_direction == Direction.Up) ? Direction.Down : Direction.Up;
                    Console.WriteLine("{0:mm:ss} - {1}.{2}: switched direction, going {3}", DateTime.Now, _id, _currFloor, _direction);
                }
            }
            else                    // Else move up/down
            {
                lock (_lockObj)
                    _currFloor += (_direction == Direction.Up ? 1 : -1);

                Thread.Sleep(c_moveTimePerFloorSecs * 1000);
            }

            return RemainingFloors();  // Return true if there are still marked floors
        }

        bool AtTopOrBottom()
        {
            // Returns true if at top/bottom physical floor.  Also returns true if at top/bottom
            // marked floor in either direction, or if no floors remain marked.

            if (_currFloor == 0 || _currFloor == _numFloors - 1)
                return true;

            if (GetMarkedTopAndBottom(out int topMarked, out int bottomMarked))
                return (_currFloor >= topMarked && _direction == Direction.Up) || (_currFloor <= bottomMarked && _direction == Direction.Down);
            else
                return true;
        }

        // Return the closest marked floor, if any.
        bool FindClosestMarkedFloor(out int closestFloor)
        {
            closestFloor = -1;
            int closestFloorDist = int.MaxValue;

            lock (_lockObj)
            {
                for (int i = 0; i < _numFloors; i++)
                {
                    if ((_floorsUp[i] || _floorsDown[i]) && Math.Abs(i - _currFloor) < closestFloorDist)
                    {
                        closestFloor = i;
                        closestFloorDist = Math.Abs(i - _currFloor);
                    }
                }
            }

            return closestFloor != -1;
        }

        // Are there any marked floors left in the current direction?
        bool RemainingFloors()
        {
            lock (_lockObj)
            {
                int increment = (_direction == Direction.Up) ? 1 : -1;
                for (int i = _currFloor; i >= 0 && i < _numFloors; i += increment)
                    if (_floorsUp[i] || _floorsDown[i]) return true;
            }

            return false;
        }

        // Get the top/bottom marked floors.
        bool GetMarkedTopAndBottom(out int topMarked, out int bottomMarked)
        {
            bool hasMarked = false;
            topMarked = int.MinValue;
            bottomMarked = int.MaxValue;

            lock (_lockObj)
            {
                for (int i = 0; i < _numFloors; i++)
                {
                    if (_floorsUp[i] || _floorsDown[i])
                    {
                        hasMarked = true;
                        if (i < bottomMarked) bottomMarked = i;
                        if (i > topMarked) topMarked = i;
                    }
                }
            }

            return hasMarked;
        }
    }
}
