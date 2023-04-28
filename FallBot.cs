using Robocode;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;


namespace CAP4053.Student
{
    public class FallBot : TeamRobot
    {
        //Math Equations
        static Tuple<double, double> QuadraticEquation(double a, double b, double c)
        {
            Console.WriteLine($"A:{a},B:{b},C:{c}");
            double underTheSquare = Math.Sqrt(Math.Pow(b, 2) - 4 * a * c);
            Console.WriteLine($"Sqrt:{underTheSquare}");
            Tuple<double, double> result = Tuple.Create((-b + underTheSquare) / (2 * a), (-b - underTheSquare) / (2 * a));
            Console.WriteLine($"+Result: {result.Item1}, -Result:{result.Item2}");

            
            return result;
        }

        //Helper Class/Enum for state
        class ScannedObj
        {
            
            public string name;
            public bool isEnemy;
            public double xObj;
            public double yObj;
            public double velocity;
            private List<Tuple<double, double>> lastTenPos;
            public double heading;
            public double dist;
            public double energy;
            public long time;
            public int missedBullets;

            

            public ScannedObj(string name, bool isEnemy, double x = 0, double y = 0, double velocity = 0, double heading = 0, double dist = 0, double energy = 100, long time = 0)
            {
                this.name = name;
                this.isEnemy = isEnemy;
                this.xObj = x;
                this.yObj = y;
                this.velocity = velocity;
                this.heading = heading;
                this.energy = energy;
                this.time = time;

                missedBullets = 0;

                lastTenPos = new List<Tuple<double,double>>();
                
                

            }

            public Tuple<double, double> estimatedNextPosition(long currTime)
            {
                long deltaTime = currTime - time;
                double nextX, nextY;
                nextX = velocity * deltaTime * Math.Sin(heading) + xObj;
                nextY = velocity * deltaTime * Math.Cos(heading) + yObj;

                return Tuple.Create(nextX, nextY);
            }

            public Tuple<double, double> predictWhereToFire(long currTime, Tuple<double, double> currUserPos, double bulletVelocity)
            {
                //Dev Notes: Tried to find this on my own first, but had forgotten the necesaary vector algebra
                //Ended up close to the answer but I needed the last step, so I got help from Math StackExchange
                //Citation for the math: https://math.stackexchange.com/a/1603699

                //Get where the enemy should be first
                Tuple<double, double> est = avgPos(50, currTime);

                if (velocity == 0)
                {
                    return est;
                }
                //Place user at the origin
                est = Tuple.Create(est.Item1 - currUserPos.Item1, est.Item2 - currUserPos.Item2);
                
                Tuple<double, double> velocityVector = Tuple.Create((Math.Sin(heading) * velocity), (Math.Cos(heading) * velocity));
                double a, b, c;
                a = Math.Pow(bulletVelocity,2) - Math.Pow(velocity,2);
                b = -2 * (velocityVector.Item1 * est.Item1 + velocityVector.Item2 * est.Item2);
                c = -((est.Item1 * est.Item1) + (est.Item2 * est.Item2));

                Tuple<double, double> quad = QuadraticEquation(a,b,c);
                long estTime;

                if (quad.Item1 > 0)
                {
                    estTime = (long)Math.Floor(quad.Item1);
                }
                else if (quad.Item2 > 0)
                {
                    estTime = (long)Math.Floor(quad.Item2);
                }
                else
                {
                    throw new Exception("Error in Bullet Prediction: " + quad.Item1 + "," + quad.Item2);
                }

                //Now that I have the time, I can acquire the velocity vector of the bullet
                est = Tuple.Create(velocityVector.Item1 + (est.Item1 / estTime), velocityVector.Item2 + (est.Item2/estTime));

                //This provides the direction for the bullet
                //Now the potential collision point is needed.
                est = Tuple.Create(est.Item1 * estTime + currUserPos.Item1, est.Item2* estTime + currUserPos.Item2);

                return est;
            }

            public void updateXY(double xNew, double yNew)
            {
                if (lastTenPos.Count == 10)
                {
                    lastTenPos.RemoveAt(0);
                }

                lastTenPos.Add(Tuple.Create(xNew, yNew));
                xObj = xNew;
                yObj = yNew;
            }

            public Tuple<double, double> avgPos(double requiredPrec, long currTime)
            {
                double avgX = 0; 
                double avgY = 0;
                foreach (Tuple<double, double> z in lastTenPos)
                {
                    avgX += z.Item1;
                    avgY += z.Item2;
                }
                avgX /= lastTenPos.Count;
                avgY /= lastTenPos.Count;

                Tuple<double, double> avgPos = Tuple.Create(avgX, avgY);
                bool useAvg = true;
                
                foreach(Tuple<double, double> z in lastTenPos)
                {
                    if (!(z.Item1 > avgX - requiredPrec && z.Item1 < avgX + requiredPrec && z.Item2 > avgY - requiredPrec && z.Item2 < avgY + requiredPrec))
                    {
                        useAvg = false;
                        break;
                    }
                }

                return (useAvg)? avgPos : estimatedNextPosition(currTime);


            }

            

        }

        private enum State
        {
            STRAFE,
            FLEE,
            RUSH,
            PANIC,
            CHASED,
            NOTHING

        }

        //Variables

        State rState = State.NOTHING;
        Dictionary<string, ScannedObj> target = new Dictionary<string, ScannedObj>();
        string currentTarget = "";
        string closestTarget = "";

        double nextX, nextY, bulletPow, aheadVelo;
        Random rng = new Random();

        //
        //Functions
        //

        //Update State

        void updateState()
        {
            DebugProperty["State"] = rState.ToString();
            if (rState != State.PANIC)
            {
                if (closestTarget != "")
                {
                    if (target[closestTarget].energy < 40)
                    {
                        rState = State.RUSH;
                    }
                    else if (target[closestTarget].dist < 60)
                    {
                        rState = State.CHASED;
                    }
                    else if (Time < 40 || target[closestTarget].dist < 250)
                    {
                        rState = State.FLEE;
                    }
                    else if (currentTarget != "" && target[currentTarget].dist > 100)
                    {
                        rState = State.STRAFE;
                    }
                }
                else
                {
                    rState = State.NOTHING;
                }
            }

            else
            {
                if (!(X < 100 || Y < 100 || X > BattleFieldWidth - 100 || Y > BattleFieldHeight - 100))
                {
                    rState = State.NOTHING;
                }
            }
        }

        
        

        //Turn enemy data from event to XY coordinate
        Tuple<double, double> getEnemyLocation(double enBearing, double enDistance, Tuple<double,double> currLoc, double myHeading)
        {
            Tuple<double, double> loc = Tuple.Create(currLoc.Item1, currLoc.Item2);
            double nextX = currLoc.Item1;
            double nextY = currLoc.Item2;
            double actualAngle = myHeading + enBearing;
            nextX += enDistance * Math.Sin(actualAngle);
            nextY += enDistance * Math.Cos(actualAngle);
            loc = Tuple.Create(nextX, nextY);

            return loc;
        }

        //Turn XY coordinates into an angle/heading
        double getTurnFromXY(double Xval, double Yval)
        {
            double turn;
            turn = Math.Atan2((Xval - X), (Yval - Y));
            return turn;
        }

        //Using heading and assuming "right" turns will only be used, calculate the best angle to turn
        double bestTurnFromHeading(double targetAngle, double headingCurr)
        {
            double bestTurn = targetAngle;
            bestTurn -= headingCurr;
            if (bestTurn < -Math.PI)
            {
                bestTurn += 2 * Math.PI;
            }
            else if (bestTurn > Math.PI)
            {
                bestTurn -= 2 * Math.PI;
            }

            
            return bestTurn;
        }

        //Using ScannedObj, aim gun towards the enemy, if aimAhead = true try to predict their next coordinate
        void turnGunToEnemy(string enemyName, bool aimAhead = false, double bulletPower = 1, double variation = 0)
        {
            if (enemyName == null || !target.ContainsKey(enemyName))
            {
                return;
            }

            ScannedObj tmp = target[enemyName];
            double turnGun;

            if (!aimAhead)
            {
                turnGun = getTurnFromXY(tmp.xObj, tmp.yObj);
            }
            else 
            {
                Tuple<double, double> predictVal = tmp.predictWhereToFire(Time, Tuple.Create(X, Y), Rules.GetBulletSpeed(bulletPower));
                turnGun = getTurnFromXY(predictVal.Item1, predictVal.Item2);
            }

            turnGun = bestTurnFromHeading(turnGun, GunHeadingRadians);
            turnGun += ((rng.NextDouble() * 2 - 1) * variation);
            SetTurnGunRightRadians(turnGun);
            bool fireWeapon = true;

            foreach (KeyValuePair<string, ScannedObj> obj in target)
            {
                if (!obj.Value.isEnemy && obj.Value.dist < tmp.dist)
                {
                    double turnFriend = getTurnFromXY(obj.Value.xObj, obj.Value.yObj);
                    turnGun = bestTurnFromHeading(turnFriend, GunHeadingRadians);
                    if (Math.Abs(turnGun - turnFriend) < 0.01f)
                    {
                        fireWeapon = false;
                    }

                }
            }
            if (fireWeapon)
            {
                SetFire(bulletPower);
            }

            
        }

        void bulletShielding(string enemyName, double variation, double bulletPower = 0.1f)
        {
            if (enemyName == null || !target.ContainsKey(enemyName))
            {
                return;
            }

            ScannedObj tmp = target[enemyName];
            

            double turnGun = getTurnFromXY(tmp.xObj, tmp.yObj);
            turnGun += (rng.NextDouble() * 2 * variation) - variation;
            turnGun = bestTurnFromHeading(turnGun, GunHeadingRadians);

            SetTurnGunRightRadians(turnGun);

            SetFire(bulletPower);
        }





        //Events
        public override void OnScannedRobot(ScannedRobotEvent evt)
        {
            if (!target.ContainsKey(evt.Name))
            {
                target.Add(evt.Name, new ScannedObj(evt.Name, !IsTeammate(evt.Name)));
            }
            ScannedObj tmp = target[evt.Name];
            Tuple<double, double> updatedLocation = getEnemyLocation(evt.BearingRadians, evt.Distance, Tuple.Create(X,Y), HeadingRadians);
            tmp.xObj = updatedLocation.Item1;
            tmp.yObj = updatedLocation.Item2;
            tmp.updateXY(tmp.xObj, tmp.yObj);
            tmp.energy = evt.Energy;
            tmp.velocity = evt.Velocity;
            tmp.heading = evt.HeadingRadians;
            tmp.dist = evt.Distance;
            tmp.time = evt.Time;

            if (currentTarget == "" && tmp.isEnemy)
            {
                currentTarget = evt.Name;
            }

            if ((closestTarget == "" || evt.Distance < target[closestTarget].dist) && tmp.isEnemy)
            {
                closestTarget = evt.Name;
            }

            
        }

        public override void OnHitWall(HitWallEvent evnt)
        {
            rState = State.PANIC;
            
        }

        public override void OnBulletMissed(BulletMissedEvent evnt)
        {
            if (rState == State.STRAFE || rState == State.RUSH)
            target[currentTarget].missedBullets++;
        }
        public override void OnBulletHit(BulletHitEvent evnt)
        {
            int mB = target[currentTarget].missedBullets;
            mB -= 1;
            if (mB < 0)
            {
                target[currentTarget].missedBullets = 0;
            }
            else
            {
                target[currentTarget].missedBullets = mB;
            }
        }

        public override void OnRobotDeath(RobotDeathEvent evnt)
        {
            target.Remove(evnt.Name);
            if (currentTarget == evnt.Name)
            {
                currentTarget = "";
            }
            if (closestTarget == evnt.Name)
            {
                closestTarget = "";
            }

        }


        //"Main" function
        public override void Run()
        {
            SetColors(Color.Orange, Color.Gray, Color.Yellow, Color.White, Color.Gold);
            IsAdjustGunForRobotTurn = true;
            IsAdjustRadarForGunTurn= true;
            IsAdjustRadarForRobotTurn = true;

            
            aheadVelo = Double.PositiveInfinity;
            SetAhead(aheadVelo);

            bulletPow = Rules.MAX_BULLET_POWER;

            
            while (true)
            {
                SetTurnRadarRight(Double.PositiveInfinity);
                ScannedObj close;
                double moveTurn;


                switch (rState)
                {
                    case State.STRAFE:

                        close = target[closestTarget];
                        nextX = close.xObj;
                        nextY = close.yObj;
                        bulletPow = Rules.MAX_BULLET_POWER /2;

                        moveTurn = getTurnFromXY(nextX, nextY) + Math.PI/2;

                        moveTurn = bestTurnFromHeading(moveTurn, HeadingRadians);

                        aheadVelo *= (rng.Next(4) >= 1) ? 1 : -1;
                        SetTurnRightRadians(moveTurn);

                        double radarTurn = getTurnFromXY(nextX, nextY);
                        radarTurn = bestTurnFromHeading(radarTurn, RadarHeadingRadians) + rng.NextDouble() - 0.5f;
                        SetTurnRadarRightRadians(radarTurn);

                        turnGunToEnemy(currentTarget, true, bulletPow);
                        currentTarget = closestTarget;

                        break;

                    case State.FLEE:
                        close = target[closestTarget];
                        nextX = close.xObj;
                        nextY = close.yObj;
                        bulletPow = Rules.MIN_BULLET_POWER;
                        aheadVelo = Double.NegativeInfinity;
                        bulletShielding(close.name, 0.2f);

                        
                        moveTurn = getTurnFromXY(nextX, nextY);

                        if (X < 100 || Y < 100 || X > BattleFieldWidth - 100 || Y > BattleFieldHeight - 100)
                        {
                            moveTurn += (rng.Next(2) >= 1) ? Math.PI / 2 : -Math.PI / 2;
                        }
                        else
                        {
                            moveTurn += (rng.NextDouble() - 1) / 2;
                        }

                        moveTurn = bestTurnFromHeading(moveTurn, HeadingRadians);
                        SetTurnRightRadians(moveTurn);

                        break;

                    case State.CHASED:
                        close = target[closestTarget];
                        nextX = close.xObj;
                        nextY = close.yObj;
                        aheadVelo = Double.NegativeInfinity;
                        bulletPow = Rules.MAX_BULLET_POWER;

                        moveTurn = getTurnFromXY(nextX, nextY);

                        moveTurn = bestTurnFromHeading(moveTurn, HeadingRadians);
                        double variation = (rng.NextDouble() - 0.5f)/4;

                        SetTurnRightRadians(moveTurn + variation);

                        turnGunToEnemy(closestTarget, true, bulletPow, 0.25f);

                        currentTarget = closestTarget;
                        break;

                    case State.RUSH:
                        close = target[closestTarget];
                        nextX = close.xObj;
                        nextY = close.yObj;
                        aheadVelo = Double.PositiveInfinity;
                        bulletPow = Rules.MAX_BULLET_POWER;

                        moveTurn = getTurnFromXY(nextX, nextY);

                        moveTurn = bestTurnFromHeading(moveTurn, HeadingRadians);
                        SetTurnRightRadians(moveTurn);

                        turnGunToEnemy(currentTarget, true, bulletPow);

                        break;

                    case State.PANIC:
                        nextX = BattleFieldWidth/4 * rng.Next(1,4);
                        nextY = BattleFieldHeight/4 * rng.Next(1,4);
                        aheadVelo = Double.PositiveInfinity;
                        bulletPow = Rules.MIN_BULLET_POWER;

                        if (closestTarget != "")
                        {
                            bulletShielding(closestTarget, 0.5f);
                        }

                        moveTurn = getTurnFromXY(nextX, nextY);
                        moveTurn = bestTurnFromHeading(moveTurn, HeadingRadians);
                        SetTurnRightRadians(moveTurn);

                        break;

                    case State.NOTHING:
                        
                        nextX = BattleFieldWidth / 2;
                        nextY = BattleFieldHeight / 2;
                        aheadVelo = Double.PositiveInfinity;
                        bulletPow = 1;

                        moveTurn = getTurnFromXY(nextX, nextY);

                        moveTurn = bestTurnFromHeading(moveTurn, HeadingRadians);
                        SetTurnRightRadians(moveTurn);

                        turnGunToEnemy(currentTarget, true, bulletPow);

                        break;

                    default:
                        break;
                }
                

                
                SetAhead(aheadVelo);

                //Necesarry Functions
                Execute();

                updateState();
            }
        }
        

    }
}
