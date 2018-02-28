using System;
using SenseNet.ContentRepository.Storage;
using SenseNet.ContentRepository;
using System.Xml.Serialization;

namespace SenseNet.DirectoryServices
{
    public enum ActionType
    {
        CreateNewADUser,
        UpdateADUser,
        CreateNewADContainer,
        UpdateADContainer,
        DeleteADObject
    }

    [XmlRootAttribute("ADAction")]
    public class ADAction
    {
        private ActionType _actionType;
        public ActionType ActionType
        {
            get { return _actionType; }
            set { _actionType = value; }
        }

        private Node _node;
        [XmlIgnore()]
        public Node Node
        {
            get { return _node ?? (_node = Node.LoadNode(_nodeId)); }
            set { _node = value; }
        }

        private int _nodeId;
        public int NodeId
        {
            get
            {
                return _node?.Id ?? _nodeId;
            }
            set { _nodeId = value; }
        }

        public string NodePath { get; set; }

        public Guid? Guid { get; set; }

        public string PassWd { get; set; }

        public string NewPath { get; set; }

        public string LastException { get; set; }

        public void Execute()
        {
            var syncPortal2AD = new SyncPortal2AD();
            switch (_actionType)
            {
                case ActionType.CreateNewADUser:
                    syncPortal2AD.CreateNewADUser((User)Node, NewPath, PassWd);
                    break;
                case ActionType.UpdateADUser:
                    syncPortal2AD.UpdateADUser((User)Node, NewPath, PassWd);
                    break;
                case ActionType.CreateNewADContainer:
                    syncPortal2AD.CreateNewADContainer(Node, NewPath);
                    break;
                case ActionType.UpdateADContainer:
                    syncPortal2AD.UpdateADContainer(Node, NewPath);
                    break;
                case ActionType.DeleteADObject:
                    syncPortal2AD.DeleteADObject(NodePath, Guid);
                    break;
            }
        }

        public ADAction(ActionType actionType, Node node, string newPath, string passWd)
        {
            _actionType = actionType;
            _node = node;
            NewPath = newPath;
            PassWd = passWd;
            NodePath = node.Path;
            Guid = Common.GetPortalObjectGuid(node);
        }

        public ADAction()
        {
        }

    }
}
