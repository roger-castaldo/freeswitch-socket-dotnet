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

    public struct sGatewayNumberPair
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
    }
}
