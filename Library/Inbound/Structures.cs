using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.FreeSwitchSockets.Inbound
{
    public struct sDomainExtensionPair
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
