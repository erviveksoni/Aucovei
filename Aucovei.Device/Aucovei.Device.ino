#include <Wire.h>  // Library which contains functions to have I2C Communication
#include "TimerOne.h"
#include <SimpleDHT.h>

#define SLAVEADDRESS 0x40 // Define the I2C address to Communicate to Uno

/*********************** Definition of motor pins ***************************/
#define pwmMotorRightPin 3
#define pwmMotorLeftPin 6
#define MotorLeft1Pin 7
#define MotorLeft2Pin 8
#define MotorRight1Pin 4
#define MotorRight2Pin 5

/*********************** Other pins ***************************/
#define FrontLEDPin 11
#define RearLEDPin 12
#define BuzzerPin 16
#define PinDHT11 9

#define STOP 1
#define TURN_FORWARD 2
#define TURN_BACK 3
#define TURN_LEFT 4
#define TURN_RIGHT 5
#define FRONT_LED_ON 6
#define FRONT_LED_OFF 7
#define REAR_LED_ON 8
#define REAR_LED_OFF 9
#define TURN_BACK_LEFT 10
#define TURN_BACK_RIGHT 11
#define FAST_SPEED 220
#define NORMAL_SPEED 160
#define TURN_SPEED 180
#define SLOW_SPEED 100
#define STOP_SPEED 0

enum command
{
  dirLeft = TURN_LEFT,
  dirRight = TURN_RIGHT,
  dirStraight = TURN_FORWARD,
  dirBack = TURN_BACK,
  dirStop = STOP,
  speedSlow = SLOW_SPEED,
  speedFast = FAST_SPEED,
  speedStop = STOP_SPEED,
  speedTurn = TURN_SPEED,
  speedNormal = NORMAL_SPEED,
  frontLedOn = FRONT_LED_ON,
  frontLedOff = FRONT_LED_OFF,
  rearLedOn = REAR_LED_ON,
  rearLedOff = REAR_LED_OFF,
  turnBackLeft = TURN_BACK_LEFT,
  turnBackRight = TURN_BACK_RIGHT
};


unsigned int counter = 0;
int rotationPerSecond = 0; // this data is sent to PI
byte temperature = 0;
byte humidity = 0;
SimpleDHT11 dht11(PinDHT11);

void incrementcount()  // counts from the speed sensor
{
  counter++;  // increase +1 the counter value
}

void trackSpeedInterrupt()
{
  Timer1.detachInterrupt();  //stop the timer
  Serial.print("Motor Speed: ");
  rotationPerSecond = (counter / 20);  // divide by number of holes in Disc
  Serial.print(rotationPerSecond, DEC);
  Serial.println(" Rotation per seconds");
  counter = 0; //  reset counter to zero
  Timer1.attachInterrupt( trackSpeedInterrupt );  //enable the timer
}

void setup()
{
  Wire.begin(SLAVEADDRESS);
  Wire.onReceive(I2CReceived);
  Wire.onRequest(I2CRequest);
  Serial.begin(9600);
  setuplogic();

  Timer1.initialize(1000000); // set timer for 1sec
  attachInterrupt(0, incrementcount, RISING);  // increase counter when speed sensor pin goes High
  Timer1.attachInterrupt( trackSpeedInterrupt ); // enable the timer
}

void loop() {
  // DHT11 sampling rate is 1HZ.
  delay(1500);

  int err = SimpleDHTErrSuccess;
  if ((err = dht11.read(&temperature, &humidity, NULL)) != SimpleDHTErrSuccess) {
    Serial.print("Read DHT11 failed, err=");
    Serial.println(err);
    delay(1000);
  }

  // Serial.print((double)temperature); Serial.print(" *C, ");
  // Serial.print((double)humidity); Serial.println(" H");
}

void setuplogic() {
  // Arduino smartcar setup
  pinMode(MotorRight1Pin, OUTPUT); // Motor pin setup
  pinMode(MotorRight2Pin, OUTPUT);
  pinMode(MotorLeft1Pin, OUTPUT);
  pinMode(MotorLeft2Pin, OUTPUT);
  pinMode(pwmMotorLeftPin, OUTPUT);
  pinMode(pwmMotorRightPin, OUTPUT);
  pinMode(FrontLEDPin, OUTPUT);
  pinMode(RearLEDPin, OUTPUT);
  pinMode(BuzzerPin, OUTPUT);
}

// function that executes whenever data is received from master
// this function is registered as an event, see setup()
void I2CReceived(int howMany) {
  String inString = "";
  while (Wire.available()) { // loop through all but the last
    int inChar = Wire.read();
    if (isDigit(inChar)) {
      // convert the incoming byte to a char and add it to the string:
      inString += (char)inChar;
    }
  }

  int cmdVal = inString.toInt();    // receive byte as an integer
  //Serial.println(cmdVal);         // print the integer
  executeCommand(cmdVal);
}

void executeCommand(int cmdval) {
  switch (cmdval)
  {
    case dirLeft:
      turnLVehicle(0);
      break;
    case dirRight:
      turnRVehicle(0);
      break;
    case dirStraight:
      advanceVehicle(0);
      break;
    case dirBack:
      reverseVehicle(0);
      break;
    case dirStop:
      stopVehicle(0);
      break;
    case speedSlow:
      setDriveSpeed(SLOW_SPEED);
      break;
    case speedFast:
      setDriveSpeed(FAST_SPEED);
      break;
    case speedStop:
      setDriveSpeed(STOP_SPEED);
      break;
    case speedTurn:
      setDriveSpeed(TURN_SPEED);
      break;
    case speedNormal:
      setDriveSpeed(NORMAL_SPEED);
      break;
    case frontLedOn:
      digitalWrite(FrontLEDPin, HIGH);
      break;
    case frontLedOff:
      digitalWrite(FrontLEDPin, LOW);
      break;
    case rearLedOn:
      digitalWrite(RearLEDPin, HIGH);
      break;
    case rearLedOff:
      digitalWrite(RearLEDPin, LOW);
      break;
    case turnBackRight:
      turnreverseRVehicle(0);
      break;
    case turnBackLeft:
      turnreverseLVehicle(0);
      break;
    default:
      //Serial.println("INVALID VALUE");
      break;
  }
}

void setDriveSpeed(int speed) {
  analogWrite(pwmMotorLeftPin, speed);
  analogWrite(pwmMotorRightPin, speed);
}

// Forward;
void advanceVehicle(int a)
{
  digitalWrite(MotorLeft1Pin, LOW);
  digitalWrite(MotorLeft2Pin, HIGH);
  digitalWrite(MotorRight1Pin, LOW);
  digitalWrite(MotorRight2Pin, HIGH);
  delay(a * 100);
}

// reverse
void reverseVehicle(int g)
{
  digitalWrite(MotorLeft1Pin, HIGH);
  digitalWrite(MotorLeft2Pin, LOW);
  digitalWrite(MotorRight1Pin, HIGH);
  digitalWrite(MotorRight2Pin, LOW);
  delay(g * 100);
}

// turn right(wheel)
void turnRVehicle(int d)
{
  digitalWrite(MotorLeft1Pin, LOW);
  digitalWrite(MotorLeft2Pin, HIGH);
  digitalWrite(MotorRight1Pin, HIGH);
  digitalWrite(MotorRight2Pin, LOW);

  delay(d * 100);
}

// reverse right
void turnreverseRVehicle(int g)
{
  digitalWrite(MotorLeft1Pin, HIGH);
  digitalWrite(MotorLeft2Pin, LOW);
  digitalWrite(MotorRight1Pin, LOW);
  digitalWrite(MotorRight2Pin, LOW);
  delay(g * 100);
}

// turn left(wheel)
void turnLVehicle(int e)
{
  digitalWrite(MotorLeft1Pin, HIGH);
  digitalWrite(MotorLeft2Pin, LOW);
  digitalWrite(MotorRight1Pin, LOW);
  digitalWrite(MotorRight2Pin, HIGH);
  delay(e * 100);
}

// reverse left
void turnreverseLVehicle(int g)
{
  digitalWrite(MotorLeft1Pin, HIGH);
  digitalWrite(MotorLeft2Pin, HIGH);
  digitalWrite(MotorRight1Pin, HIGH);
  digitalWrite(MotorRight2Pin, LOW);
  delay(g * 100);
}

// Stop
void stopVehicle(int f)
{
  digitalWrite(MotorRight1Pin, LOW);
  digitalWrite(MotorRight2Pin, LOW);
  digitalWrite(MotorLeft1Pin, LOW);
  digitalWrite(MotorLeft2Pin, LOW);
  delay(f * 100);
}

void I2CRequest() {
  char wiredata[20];
  String data =  String(rotationPerSecond);
  data.concat("|");
  data.concat(String((double)temperature));
  data.concat("|");
  data.concat(String((double)humidity));
  data.toCharArray(wiredata, data.length() + 1);
  Wire.write(wiredata); // return data to PI
}
