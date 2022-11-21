using Elevator.Logic;
using Elevator.Models;
using System;
using System.IO;
using System.Threading;

namespace Elevator
{
	class Program
	{
		static void Main(string[] args)
		{
			// This code reads input commands from a text file and passes them along to an
			// elevator controller object.
			string path = args.Length > 0 ? args[0] : System.IO.Path.GetFullPath("Input.txt");
			string[] lines = File.ReadAllLines(path);
			int max_pass = 1;

			ElevatorStimulator es = new ElevatorStimulator();

			for (int i = 0; i < lines.Length; i++)
			{
				string[] cmd = lines[i].ToLower().Split(" ".ToCharArray());
				
				try
				{
					if (cmd[0] == "init")				// init {numElevs} {numFloors} 				- Initializes the ElevatorController
					{
						max_pass = int.Parse(cmd[2]);
						es.Start(int.Parse(cmd[1]), int.Parse(cmd[2]));
					}
					else if (cmd[0] == "sleep")			// sleep {numSecs}							- sleeps this thread for numSecs
					{
						Thread.Sleep(int.Parse(cmd[1]) * 1000);
					}
					else if (cmd[0] == "person")			// rider {name} {startFloor} {destFloor}	- submits a request for a rider
					{
						Person rider		= new Person() { _name = cmd[1], _destFloor = int.Parse(cmd[3]) };
						int startFloor	= int.Parse(cmd[2]);
						es.Floors[startFloor].AddPerson(rider);
						ElevatorLogic.Direction dir = rider._destFloor > startFloor ? ElevatorLogic.Direction.Up : ElevatorLogic.Direction.Down;
						es.RequestElevator(startFloor, dir);
					}
					else if (cmd[0] == "quit")			// quit the app
					{
						break;
					}
					else
					{
						// Ignore all other input (like comments)
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("{0:mm:ss} - Exception on line {1}: {2}", DateTime.Now, i, ex);
				}
			}

			es.Stop();
		}
	}
}
