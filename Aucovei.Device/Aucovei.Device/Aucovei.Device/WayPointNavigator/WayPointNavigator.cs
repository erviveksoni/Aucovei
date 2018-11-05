using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aucovei.Device.Compass;
using Aucovei.Device.Gps;
using Aucovei.Device.Helper;
using Aucovei.Device.RfcommService;

namespace Aucovei.Device.WayPointNavigator
{
    public class WayPointNavigator : BaseService
    {
        private Gps.GpsInformation gpsInformation;
        private HMC5883L compass;
        private CommandProcessor.CommandProcessor commandProcessor;
        public PositionInfo CurrentGpsPosition { get; private set; }

        public WayPointNavigator(
            Gps.GpsInformation gpsInformation,
            HMC5883L compass,
            CommandProcessor.CommandProcessor commandProcessor)
        {
            this.gpsInformation = gpsInformation ?? throw new ArgumentNullException(nameof(gpsInformation));
            this.compass = compass ?? throw new ArgumentNullException(nameof(compass));

            this.gpsInformation.DataReceivedEventHandler += this.GpsInformation_DataReceivedEventHandler;

            this.commandProcessor = commandProcessor;
        }

        private void GpsInformation_DataReceivedEventHandler(object sender, GpsInformation.GpsDataReceivedEventArgs e)
        {
            this.CurrentGpsPosition = e.positionInfo;
        }

        private async Task AcquireActiveGpsConnectionAsync()
        {
            //while (this.currentGpsStatus != GpsInformation.GpsStatus.Active &&
            //       !this.navigationCancellationTokenSource.IsCancellationRequested)
            //{
            //    await Task.Delay(TimeSpan.FromMilliseconds(100), this.navigationCancellationTokenSource.Token);
            //}

            return;
        }

        private List<GeoCoordinate> wayPoints = new List<GeoCoordinate>()
        {
            //new GeoCoordinate(17.455667,78.3009718),
            //new GeoCoordinate(17.4556962,78.3012658)

            new GeoCoordinate(17.4550259998007,78.3009775030915),
            new GeoCoordinate(17.455026,78.300695)
        };

        private int nextWayPointIndex = -1;
        private GeoCoordinate nextWayPoint;
        private const double WAYPOINT_DIST_TOLERANCE_METERS = 5.0;
        private double HEADING_TOLERANCE_DEGREES = 5.0;
        private CancellationTokenSource navigationCancellationTokenSource;

        public async Task StartWayPointNavigationAsync(List<GeoCoordinate> wayPoints)
        {
            if (wayPoints == null || wayPoints.Count < 1)
            {
                throw new ArgumentNullException(nameof(wayPoints));
            }

            this.navigationCancellationTokenSource = new CancellationTokenSource();

            NotifyUIEventArgs notifyEventArgs = null;

            notifyEventArgs = new NotifyUIEventArgs()
            {
                NotificationType = NotificationType.ControlMode,
                Name = "navigation",
                Data = "Navigation"
            };

            this.NotifyUIEvent(notifyEventArgs);

            this.nextWayPointIndex = -1;
            this.dummycounter = 0;
            this.dummyIndex = 0;
            this.currentPosition = null;
            this.mockwayPoints = this.GetMockWayPoints();

            // first waypoint
            this.nextWayPoint = this.GetNextWayPoint();
            if (this.nextWayPoint == null)
            {
                notifyEventArgs = new NotifyUIEventArgs()
                {
                    NotificationType = NotificationType.Console,
                    Data = "Last waypoint reached!"
                };

                this.NotifyUIEvent(notifyEventArgs);
            }

            this.StartRoverAsync();
        }

        private async void StartRoverAsync()
        {
            await this.AcquireActiveGpsConnectionAsync();

            // initial direction
            await this.commandProcessor.ExecuteCommandAsync(Commands.SpeedNormal);

            var tasks = new List<Task>()
            {
                this.MoveRoverAsync(),
                this.UpdateDesiredTurnDirectionAsync(),
                this.LoadNextWayPointAsync()
            };

            // execute asynchronously till its cancelled
            await Task.WhenAll(tasks);

            await this.commandProcessor.ExecuteCommandAsync(Commands.SpeedStop);

            NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
            {
                NotificationType = NotificationType.Console,
                Data = "All waypoint completed!"
            };

            this.NotifyUIEvent(notifyEventArgs);

            notifyEventArgs = new NotifyUIEventArgs()
            {
                NotificationType = NotificationType.ControlMode,
                Name = "Parked",
                Data = "Parked"
            };

            this.NotifyUIEvent(notifyEventArgs);
        }

        private async Task LoadNextWayPointAsync()
        {
            while (!this.navigationCancellationTokenSource.Token.IsCancellationRequested)
            {
                await this.AcquireActiveGpsConnectionAsync();

                var distanceToTarget = this.CalculateDistanceToWayPointMeters();
                if (distanceToTarget <= WAYPOINT_DIST_TOLERANCE_METERS)
                {
                    this.nextWayPoint = this.GetNextWayPoint();

                    // No next waypoint and we are enough close to the target
                    if (this.nextWayPoint == null)
                    {
                        this.navigationCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(1));
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(2000));
            }
        }

        private GeoCoordinate GetNextWayPoint()
        {
            this.nextWayPointIndex++;
            if (this.nextWayPointIndex == this.wayPoints.Count)
            {
                return null;
            }

            return this.wayPoints[this.nextWayPointIndex];
        }

        private Commands.DrivingDirection turnDirection;
        private const int SAFE_DISTANCE = 72;
        private const int TURN_DISTANCE = 36;
        private const int STOP_DISTANCE = 12;

        private async Task MoveRoverAsync()
        {
            while (!this.navigationCancellationTokenSource.Token.IsCancellationRequested)
            {
                await this.AcquireActiveGpsConnectionAsync();

                switch (this.turnDirection)
                {
                    case Commands.DrivingDirection.Forward:
                        await this.commandProcessor.ExecuteCommandAsync(Commands.SpeedNormal);
                        await this.commandProcessor.ExecuteCommandAsync(Helpers.MapDrivingDirectionToCommand(this.turnDirection));
                        break;
                    case Commands.DrivingDirection.Reverse:
                        await this.commandProcessor.ExecuteCommandAsync(Commands.SpeedNormal);
                        await this.commandProcessor.ExecuteCommandAsync(Helpers.MapDrivingDirectionToCommand(this.turnDirection));
                        break;
                    case Commands.DrivingDirection.Left:
                        await this.commandProcessor.ExecuteCommandAsync(Commands.SpeedNormal);
                        await this.commandProcessor.ExecuteCommandAsync(Helpers.MapDrivingDirectionToCommand(this.turnDirection));
                        break;
                    case Commands.DrivingDirection.Right:
                        await this.commandProcessor.ExecuteCommandAsync(Commands.SpeedNormal);
                        await this.commandProcessor.ExecuteCommandAsync(Helpers.MapDrivingDirectionToCommand(this.turnDirection));
                        break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(2000));
            }
        }

        private async void GetDesiredTurnDirection()
        {
            if (this.nextWayPoint == null)
            {
                return;
            }

            this.IncrementCoordinates();

            GeoCoordinate currentPosition = this.currentPosition;

            var targetHeading = WayPointHelper.DegreeBearing(currentPosition, this.nextWayPoint);

            // calculate where we need to turn to head to destination
            var headingError = targetHeading -
                               this.compass.GetHeadingInDegrees(this.compass.ReadRaw());

            // adjust for compass wrap
            if (headingError < -180)
            {
                headingError += 360;
            }

            if (headingError > 180)
            {
                headingError -= 360;
            }

            // calculate which way to turn to intercept the targetHeading
            if (Math.Abs(headingError) <= this.HEADING_TOLERANCE_DEGREES)
            {
                // if within tolerance, don't turn
                this.turnDirection = Commands.DrivingDirection.Forward;
            }
            else if (headingError < 0)
            {
                this.turnDirection = Commands.DrivingDirection.Left;
            }
            else if (headingError > 0)
            {
                this.turnDirection = Commands.DrivingDirection.Right;
            }
            else
            {
                this.turnDirection = Commands.DrivingDirection.Forward;
            }

            this.dummyIndex++;

            NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
            {
                NotificationType = NotificationType.Console,
                Data = "Turn Direction -  " + this.turnDirection
            };

            this.NotifyUIEvent(notifyEventArgs);
        }

        private async Task UpdateDesiredTurnDirectionAsync()
        {
            while (!this.navigationCancellationTokenSource.Token.IsCancellationRequested)
            {
                await this.AcquireActiveGpsConnectionAsync();

                this.GetDesiredTurnDirection();

                await Task.Delay(TimeSpan.FromMilliseconds(2000));
            }
        }

        //void moveAndAvoid()
        //{
        //    if (fwddistance >= SAFE_DISTANCE)       // no close objects in front of car
        //    {
        //        //Serial.println("SAFE_DISTANCE");

        //        if (this.turnDirection == straight)
        //        {
        //            speed = FAST_SPEED;
        //        }
        //        else
        //        {
        //            speed = TURN_SPEED;
        //        }

        //        setDriveSpeed(speed);
        //        setDirection(this.turnDirection);
        //    }
        //    else if (fwddistance > TURN_DISTANCE && fwddistance < SAFE_DISTANCE)    // not yet time to turn, but slow down
        //    {
        //        //Serial.println("BELOW SAFE_DISTANCE");

        //        if (this.turnDirection == straight)
        //        {
        //            speed = NORMAL_SPEED;
        //        }
        //        else
        //        {
        //            speed = TURN_SPEED;
        //            setDirection(this.turnDirection);
        //        }

        //        setDriveSpeed(speed);
        //    }
        //    else if (fwddistance < TURN_DISTANCE && fwddistance > STOP_DISTANCE)  // getting close, time to turn to avoid object
        //    {
        //        speed = SLOW_SPEED;
        //        setDriveSpeed(speed);

        //        //Serial.println("TURN_DISTANCE");
        //        switch (this.turnDirection)
        //        {
        //            case straight:  // going straight currently, so start new turn
        //                if (headingError <= 0)
        //                {
        //                    this.turnDirection = directions::left;
        //                }
        //                else
        //                {
        //                    this.turnDirection = directions::right;
        //                }

        //                setDirection(this.turnDirection);
        //                break;
        //            case left:
        //                setDirection(directions::right);// if already turning left, try right
        //                break;
        //            case right:
        //                setDirection(directions::left);// if already turning left, try elft
        //                break;
        //        } // end SWITCH
        //    }
        //    else if (fwddistance < STOP_DISTANCE)          // too close, stop and back up
        //    {
        //        //Serial.println("STOP_DISTANCE");
        //        setDirection(directions::stopcar); // stop
        //        setDriveSpeed(STOP_SPEED);
        //        delay(10);
        //        this.turnDirection = directions::straight;
        //        setDirection(directions::back); // drive reverse
        //        setDriveSpeed(NORMAL_SPEED);
        //        delay(200);
        //        while (fwddistance < TURN_DISTANCE)       // backup until we get safe clearance
        //        {
        //            //Serial.print("STOP_DISTANCE CALC: ");
        //            //Serial.print(sonarDistance);
        //            //Serial.println();
        //            //if (GPS.parse(GPS.lastNMEA()))
        //            currentHeading = getComapssHeading();    // get our current heading
        //                                                     //processGPSData();
        //            calcDesiredTurn();// calculate how we would optimatally turn, without regard to obstacles
        //            fwddistance = checkSonarManual();
        //            updateDisplay();
        //            delay(100);
        //        } // while (sonarDistance < TURN_DISTANCE)

        //        setDirection(directions::stopcar);        // stop backing up
        //    } // end of IF TOO CLOSE
        //}   // moveAndAvoid()

        private double CalculateDistanceToWayPointMeters()
        {
            GeoCoordinate currentPosition = this.currentPosition;

            var distanceToTarget = WayPointHelper.DistanceBetweenGeoCoordinate(
                currentPosition,
                this.nextWayPoint,
                WayPointHelper.DistanceType.Meters);

            return distanceToTarget;
        }

        private int dummycounter = 0;

        private List<GeoCoordinate> mockwayPoints;

        private List<GeoCoordinate> GetMockWayPoints()
        {
            return mockwayPoints = new List<GeoCoordinate>()
            {
                new GeoCoordinate(17.4550259998007, 78.3009775030915),
                new GeoCoordinate(17.4550259998386, 78.3009492527823),
                new GeoCoordinate(17.4550259998725, 78.3009210024732),
                new GeoCoordinate(17.4550259999024, 78.300892752164),
                new GeoCoordinate(17.4550259999283, 78.3008645018549),
                new GeoCoordinate(17.4550259999502, 78.3008362515457),
                new GeoCoordinate(17.4550259999681, 78.3008080012366),
                new GeoCoordinate(17.4550259999821, 78.3007797509274),
                new GeoCoordinate(17.455025999992, 78.3007515006183),
                new GeoCoordinate(17.455025999998, 78.3007232503091)
            };
        }

        private GeoCoordinate GetDummyPoints()
        {
            if (this.dummycounter >= 1)
            {
                this.mockwayPoints.RemoveAt(0);
                this.dummycounter = 0;
            }
            else
            {
                this.dummycounter += 1;
            }

            var result = this.mockwayPoints.FirstOrDefault();

            return result;
        }

        private int dummyIndex = 0;
        GeoCoordinate currentPosition = null;

        private void IncrementCoordinates()
        {
            //GeoCoordinate currentPosition = new GeoCoordinate(
            //    this.CurrentGpsPosition.Latitude.Value,
            //    this.CurrentGpsPosition.Longitude.Value);

            if (this.dummyIndex < 19)
            {
                this.dummyIndex++;
                this.currentPosition = this.GetDummyPoints();
            }
            else
            {
                this.currentPosition = this.mockwayPoints.LastOrDefault();
            }

            NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
            {
                NotificationType = NotificationType.Console,
                Data = "Current GPS As - " + this.currentPosition.ToString()
            };

            this.NotifyUIEvent(notifyEventArgs);
        }
    }
}
