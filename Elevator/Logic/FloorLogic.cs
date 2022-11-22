using Elevator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elevator.Logic
{
    public class FloorLogic
    {
        public int m_floor;
        public List<Person> m_Persons;

        public FloorLogic(int floor)
        {
            m_floor = floor;
            m_Persons = new List<Person>();
        }

        public void AddPerson(Person Person)
        {
            lock (m_Persons)
                m_Persons.Add(Person);
        }

        public void ElevatorArrived(ElevatorLogic elev, ElevatorLogic.Direction elevDir)
        {
            
            lock (m_Persons)
            {
                List<Person> unloadedPersons = elev.UnloadPersons();

                for (int i = m_Persons.Count - 1; i >= 0; i--)
                {
                    Person Person = m_Persons[i];
                    ElevatorLogic.Direction PersonDir = (Person._destFloor > m_floor) ? ElevatorLogic.Direction.Up : ElevatorLogic.Direction.Down;
                    if (PersonDir == elevDir && Person._destFloor != m_floor)
                    {
                        elev.LoadPerson(Person);
                        m_Persons.RemoveAt(i);
                    }
                }

                // Add unloaded Persons back to floor.
                for (int i = 0; i < unloadedPersons.Count; i++)
                {

                    AddPerson(unloadedPersons[i]);
                }
                    
            }
        }
    }
}
