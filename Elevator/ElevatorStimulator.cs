using Elevator.Logic;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Elevator
{

    public class ElevatorStimulator
    {
        class FloorRequest
        {
            public int m_floor;
            public ElevatorLogic.Direction m_dir;

            public FloorRequest(int floor, ElevatorLogic.Direction dir) { m_floor = floor; m_dir = dir; }
        }

        const int c_defaultRunIntervalMsecs = 1000;     // Sleep interval when request can't be immediately serviced.

        internal FloorLogic[] Floors;            // List of floors
        internal ElevatorLogic[] Elevators;      // The bank of elevators

        bool m_running;
        Queue<FloorRequest> m_requests;     // List of outstanding elevator requests from floors.

        public void Start(int numFloors, int numElevators)
        {
            Floors = new FloorLogic[numFloors];
            for (int i = 0; i < numFloors; i++)
                Floors[i] = new FloorLogic(i);

            Elevators = new ElevatorLogic[numElevators];
            for (int i = 0; i < numElevators; i++)
            {
                Elevators[i] = new ElevatorLogic(Convert.ToChar(i + 65), numFloors);     // Give each elevator a letter as ID
                Elevators[i].OnElevatorEvent += HandleElevatorEvent;
            }

            // Startup thread to handle floor requests
            m_requests = new Queue<FloorRequest>();
            Thread thread = new Thread(new ThreadStart(this.Run));
            thread.Start();

            // Startup elevator threads
            for (int i = 0; i < numElevators; i++)
            {
                thread = new Thread(new ThreadStart(Elevators[i].Run));
                thread.Start();
            }
        }

        public void Stop()
        {
            for (int i = 0; i < Elevators.Length; i++)
                Elevators[i].StopElevator();

            m_running = false;
        }

        public void RequestElevator(int floor, ElevatorLogic.Direction dir)
        {
            lock (m_requests)
                m_requests.Enqueue(new FloorRequest(floor, dir));
        }

        void Run()
        {
            // This is the main loop for the ElevatorController thread.  It attempts to service
            // requests from the queue in order.  If one can't be serviced, the thread sleeps
            // for 1 second, then tries again.

            m_running = true;
            while (m_running)
            {
                while (m_requests.Count > 0)
                {
                    bool handled = false;
                    lock (m_requests)
                    {
                        FloorRequest req = m_requests.Peek();       // Peek at next floor request
                        int idx = FindBestElevator(req.m_floor, req.m_dir);
                        if (idx >= 0)
                        {
                            m_requests.Dequeue();   // Dequeue the request and send to elevator
                            Elevators[idx].RequestFloor(req.m_floor, req.m_dir);
                            handled = true;
                        }
                    }

                    // If none handled, sleep for a bit to allow elevators to continue their work.
                    if (!handled)
                        Thread.Sleep(c_defaultRunIntervalMsecs);
                }
            }
        }

        int FindBestElevator(int floor, ElevatorLogic.Direction dir)
        {
            // This method chooses which elevator to route a particular request to.  It first finds the closest
            // idle elevator.  Then it finds the closest one moving in the proper direction (and above/below as necessary).
            // It then selects whichever of these is closest.  If none are found, the method returns -1.

            bool goingUp = dir == ElevatorLogic.Direction.Up;
            int closestIdleIdx = -1, closestIdleDist = int.MaxValue;
            int closestMovingIdx = -1, closestMovingDist = int.MaxValue;

            for (int i = 0; i < Elevators.Length; i++)
            {
                Elevators[i].GetElevatorState(out ElevatorLogic.State elevState, out ElevatorLogic.Direction elevDir, out int elevFloor);
                int dist = Math.Abs(floor - elevFloor);

                if (elevState == ElevatorLogic.State.Idle)
                {
                    if (dist < closestIdleDist)
                    {
                        closestIdleIdx = i;
                        closestIdleDist = dist;
                    }
                }
                else if ((elevDir == dir) && (goingUp ? (elevFloor <= floor) : (elevFloor >= floor)))
                {
                    if (dist < closestMovingDist)
                    {
                        closestMovingIdx = i;
                        closestMovingDist = dist;
                    }
                }
            }

            int bestIdx;
            if (closestIdleIdx == -1 && closestMovingIdx == -1)     // If no closest idle nor moving, return none found
                bestIdx = -1;
            else if (closestMovingIdx == -1)                        // If no closest moving, use closest idle
                bestIdx = closestIdleIdx;
            else if (closestIdleIdx == -1)                          // If no closest idle, use closest moving
                bestIdx = closestMovingIdx;
            else
                bestIdx = closestMovingDist < closestIdleDist ? closestMovingIdx : closestIdleIdx;  // If both, choose closer one

            return bestIdx;
        }

        void HandleElevatorEvent(ElevatorLogic elev, ElevatorLogic.State state, int floor, ElevatorLogic.Direction dir)
        {
            if (state == ElevatorLogic.State.VisitingFloor)
                Floors[floor].ElevatorArrived(elev, dir);
        }
    }
}