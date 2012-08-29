using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.FreeSwitchSockets.Inbound
{
    public struct sDomainExtensionPair : IComparable
    {
        private string _extension;
        public string Extension
        {
            get { return _extension; }
        }

        private string _domain;
        public string Domain
        {
            get { return _domain; }
        }

        public sDomainExtensionPair(string extension, string domain)
        {
            _extension = extension;
            _domain = domain;
        }

        public static bool operator ==(sDomainExtensionPair x, sDomainExtensionPair y)
        {
            return (((object)x == null && (object)y == null) ? true : (((object)x != null && (object)y != null) ? x.CompareTo(y) == 0 : false));
        }

        public static bool operator !=(sDomainExtensionPair x, sDomainExtensionPair y)
        {
            return !(x == y);
        }

        public static bool operator <(sDomainExtensionPair x, sDomainExtensionPair y)
        {
            return x.CompareTo(y) < 0;
        }

        public static bool operator <=(sDomainExtensionPair x, sDomainExtensionPair y)
        {
            return x.CompareTo(y) <= 0;
        }

        public static bool operator >(sDomainExtensionPair x, sDomainExtensionPair y)
        {
            return x.CompareTo(y) > 0;
        }

        public static bool operator >=(sDomainExtensionPair x, sDomainExtensionPair y)
        {
            return x.CompareTo(y) >= 0;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return this.CompareTo(obj) == 0;
        }

        public static explicit operator sDomainExtensionPair(string formattedString)
        {
            if (!formattedString.Contains("@"))
                throw new Exception("Unable to parse Domain Extension Pair from formatted string[" + formattedString + "]");
            return new sDomainExtensionPair(formattedString.Substring(0, formattedString.IndexOf("@")),
            formattedString.Substring(formattedString.IndexOf("@")+1));
        }

        public override string ToString()
        {
            return _extension + "@" + _domain;
        }

        #region IComparable Members

        public int CompareTo(object obj)
        {
            sDomainExtensionPair dep = (sDomainExtensionPair)obj;
            if (dep.Domain == Domain)
                return Extension.CompareTo(dep.Extension);
            return Domain.CompareTo(dep.Domain);
        }

        #endregion
    }

    public struct sGatewayNumberPair : IComparable
    {
        private string _number;
        public string Number
        {
            get { return _number; }
        }

        private string _gatewayName;
        public string GatewayName
        {
            get { return _gatewayName; }
        }

        public sGatewayNumberPair(string number,string gatewayName)
        {
            _number = number;
            _gatewayName = gatewayName;
        }

        public static bool operator ==(sGatewayNumberPair x, sGatewayNumberPair y)
        {
            return (((object)x == null && (object)y == null) ? true : (((object)x != null && (object)y != null) ? x.CompareTo(y) == 0 : false));
        }

        public static bool operator !=(sGatewayNumberPair x, sGatewayNumberPair y)
        {
            return !(x == y);
        }

        public static bool operator <(sGatewayNumberPair x, sGatewayNumberPair y)
        {
            return x.CompareTo(y) < 0;
        }

        public static bool operator <=(sGatewayNumberPair x, sGatewayNumberPair y)
        {
            return x.CompareTo(y) <= 0;
        }

        public static bool operator >(sGatewayNumberPair x, sGatewayNumberPair y)
        {
            return x.CompareTo(y) > 0;
        }

        public static bool operator >=(sGatewayNumberPair x, sGatewayNumberPair y)
        {
            return x.CompareTo(y) >= 0;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return this.CompareTo(obj) == 0;
        }

        public static explicit operator sGatewayNumberPair(string formattedString)
        {
            if (!formattedString.Contains("\t"))
                throw new Exception("Unable to parse Gateway Number Pair from formatted string[" + formattedString + "]");
            return new sGatewayNumberPair(formattedString.Substring(0, formattedString.IndexOf("\t")),
            formattedString.Substring(formattedString.IndexOf("\t") + 1));
        }

        public override string ToString()
        {
            return _number + "\t" + _gatewayName;
        }

        #region IComparable Members

        public int CompareTo(object obj)
        {
            sGatewayNumberPair gnp = (sGatewayNumberPair)obj;
            if (gnp.GatewayName == GatewayName)
                return Number.CompareTo(gnp.Number);
            return GatewayName.CompareTo(gnp.GatewayName);
        }

        #endregion
    }
}
