
namespace XRUIOS.Barebones.Interfaces
{
    public static class GeoClass
    {


        public struct Coordinate
        {
            public double X, Y;

            public Coordinate() { }
            public Coordinate(double x, double y)
            {
                X = x; Y = y;
            }
        }

        public struct LocationPoint
        {
            public DateTime TimeStamp;
            public double Latitude;
            public double Longitude;

            public LocationPoint() { }


            public LocationPoint(DateTime timestamp, double latitude, double longitude)
            {
                TimeStamp = timestamp;
                Latitude = latitude;
                Longitude = longitude;
            }
        }

        //Use GeoClue on Linux
   

        public struct RelativePoint
        {
            public double latmin;
            public double latmax;
            public double longmin;
            public double longmax;
            public RelativePoint() { }


            public RelativePoint(double latmin, double latmax, double longmin, double longmax)
            {
                this.latmin = latmin;
                this.latmax = latmax;
                this.longmin = longmin;
                this.longmax = longmax;
            }
        }

        public struct RelativeLocationPoint
        {
            public DateTime Timestamp;
            public RelativePoint Area;

            public RelativeLocationPoint() { }


            public RelativeLocationPoint(DateTime timestamp, RelativePoint area)
            {
                Timestamp = timestamp;
                Area = area;
            }
        }

    }
}
