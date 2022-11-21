using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elevator.Models
{
    public class Floor
    {
        public int m_floor;
        public List<Person> m_Persons;
        public Floor(int floor)
        {
            m_floor = floor;
            m_Persons = new List<Person>();
        }

    }
}
