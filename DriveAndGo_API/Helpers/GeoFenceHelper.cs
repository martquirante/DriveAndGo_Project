using System;
using System.Collections.Generic;

namespace DriveAndGo_API.Helpers
{
    public class GeoPoint
    {
        public double Lat { get; set; }
        public double Lng { get; set; }

        public GeoPoint(double lat, double lng)
        {
            Lat = lat;
            Lng = lng;
        }
    }

    public static class GeoFenceHelper
    {
        /// <summary>
        /// Checks if a point is inside a polygon using the Ray Casting algorithm.
        /// </summary>
        public static bool IsPointInPolygon(GeoPoint point, List<GeoPoint> polygon)
        {
            if (polygon == null || polygon.Count < 3) return false;

            bool isInside = false;
            int j = polygon.Count - 1;

            for (int i = 0; i < polygon.Count; i++)
            {
                if ((polygon[i].Lng < point.Lng && polygon[j].Lng >= point.Lng || 
                     polygon[j].Lng < point.Lng && polygon[i].Lng >= point.Lng) &&
                    (polygon[i].Lat + (point.Lng - polygon[i].Lng) / (polygon[j].Lng - polygon[i].Lng) * (polygon[j].Lat - polygon[i].Lat) < point.Lat))
                {
                    isInside = !isInside;
                }
                j = i;
            }

            return isInside;
        }

        /// <summary>
        /// Checks if a point is inside a circle using the Haversine formula for distance.
        /// </summary>
        public static bool IsPointInCircle(GeoPoint point, GeoPoint center, double radiusKm)
        {
            double dLat = ToRadians(point.Lat - center.Lat);
            double dLng = ToRadians(point.Lng - center.Lng);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(center.Lat)) * Math.Cos(ToRadians(point.Lat)) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double distanceKm = 6371 * c; // Earth radius in KM

            return distanceKm <= radiusKm;
        }

        private static double ToRadians(double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }
    }
}
