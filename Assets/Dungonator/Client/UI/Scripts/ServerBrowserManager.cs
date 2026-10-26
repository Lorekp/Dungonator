#if false

using System.Collections.Generic;
using FishNet;
using Steamworks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dungonator.Client.UI.Scripts
{
    public class ServerBrowserManager : MonoBehaviour
    {
        private bool _isRefreshing;
        private Label _lblStatus;
        private readonly List<gameserveritem_t> _serverItems = new();

        /* ---------- UI ---------- */
        [Header("UI")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset rowTemplate;

        private Button _btnRefresh;

        private ListView _serverListView;

        /* ---------- Steam ---------- */
        private ISteamMatchmakingServerListResponse _responseLinker;
        private HServerListRequest _listHandle;
        private MatchMakingKeyValuePair_t[] _filters;
        private AppId_t _appId;

        public void Init()
        {
            _appId = SteamUtils.GetAppID();

            var root = uiDocument.rootVisualElement;
            _serverListView = root.Q<ListView>("serverList");
            _btnRefresh = root.Q<Button>("btnRefresh");
            _lblStatus = root.Q<Label>("lblStatus");

            _serverListView.makeItem   = () => rowTemplate.CloneTree();
            _serverListView.bindItem   = BindServerRow;
            _serverListView.itemsSource = _serverItems;
            _serverListView.selectionType = SelectionType.Single;

            _btnRefresh.clicked += RefreshServerList;
            _serverListView.itemsChosen += OnItemsChosen;

            RefreshServerList();
        }

        private void OnItemsChosen(IEnumerable<object> chosen)
        {
            foreach (var obj in chosen)          // ListView can return several, but we allow only one
            {
                if (obj is gameserveritem_t entry)
                {
                    JoinServer(entry);
                    break;
                }
            }
        }

        private void JoinServer(gameserveritem_t entry)
        {
            var nm        = InstanceFinder.NetworkManager;
            var transport = nm.TransportManager.GetTransport<FishySteamworks.FishySteamworks>();
            string adr = entry.m_NetAdr.GetConnectionAddressString(); // "10.0.20.5:27015"
            Debug.Log($"[JoinServer] IP mode – raw address string = \"{adr}\"");

            int idx = adr.LastIndexOf(':');
            if (idx < 0)
            {
                Debug.LogError($"[JoinServer] Malformed address (no colon): \"{adr}\"");
                return;
            }

            string ip = adr[..idx];
            if (!ushort.TryParse(adr[(idx + 1)..], out ushort port))
            {
                Debug.LogError($"[JoinServer] Port segment \"{adr[(idx + 1)..]}\" " +
                               $"is not a valid ushort.");
                return;
            }

            transport.SetClientAddress(ip);
            transport.SetPort(port);

            Debug.Log($"[JoinServer] Connecting to {ip}:{port} …");
            nm.ClientManager.StartConnection();

            /*var nm = InstanceFinder.NetworkManager;

            // 1. Grab the server’s SteamID that came back in the query results
            string hostSteamId = entry.m_steamID.m_SteamID.ToString();

            // 2. Connect
            nm.ClientManager.StartConnection(hostSteamId);       */ // TODO: test on 2 machines
        }

        private void RefreshServerList()
        {
            if (_isRefreshing)
                return;

            _isRefreshing = true;
            _btnRefresh.SetEnabled(false);
            _serverListView.SetEnabled(false);

            _serverItems.Clear();
            _serverListView.RefreshItems();
            _lblStatus.text = "Refreshing...";

            //cancel any still running request
            if (_listHandle != HServerListRequest.Invalid)
            {
                SteamMatchmakingServers.CancelQuery(_listHandle);
                SteamMatchmakingServers.ReleaseRequest(_listHandle);
                _listHandle = HServerListRequest.Invalid;
            }

            /* -- build filter array (none for now) -- */
            var dFilters = new Dictionary<string, string>(); // TODO add filters
            var array    = new MatchMakingKeyValuePair_t[dFilters.Count];
            int i = 0;
            foreach (var kv in dFilters)
                array[i++] = new MatchMakingKeyValuePair_t { m_szKey = kv.Key, m_szValue = kv.Value };

            _filters = array;    // <-- IMPORTANT

            /* -- hook callbacks and fire the request -- */
            _responseLinker = new ISteamMatchmakingServerListResponse(
                OnServerResponded,
                OnServerFailedToRespond,
                OnRefreshComplete);

            //_listHandle = SteamMatchmakingServers.RequestInternetServerList( TODO: LAN/Internet button?
            //                    _appId, _filters, _responseLinker);
            _listHandle = SteamMatchmakingServers.RequestLANServerList(
                                _appId, _responseLinker);

            Debug.Log($"[Browser] Query started for AppID {_appId} (handle {_listHandle}).");
        }

        /* ---------- Binding ---------- */
        private void BindServerRow(VisualElement row, int index)
        {
            var item = _serverItems[index];
            row.Q<Label>("ServerName").text  = item.GetServerName();
            row.Q<Label>("PlayerCount").text = $"{item.m_nPlayers}/{item.m_nMaxPlayers}";
            row.Q<Label>("PingMs").text      = $"{item.m_nPing} ms";
        }

        /* ---------- Callbacks ---------- */
        private void OnServerResponded(HServerListRequest req, int idx)
        {
            var det = SteamMatchmakingServers.GetServerDetails(req, idx);
            Debug.Log($"[Browser] ► {det.GetServerName()}  {det.m_nPlayers}/{det.m_nMaxPlayers}  {det.m_nPing} ms");
        }

        private void OnServerFailedToRespond(HServerListRequest req, int idx)
        {
            Debug.LogWarning($"[Browser]   ✖ Server index {idx} failed to respond.");
        }

        private void OnRefreshComplete(HServerListRequest req, EMatchMakingServerResponse resp)
        {
            Debug.Log($"[Browser] Refresh complete ({resp}); total servers: {SteamMatchmakingServers.GetServerCount(req)}.");

            var count = SteamMatchmakingServers.GetServerCount(req);
            for (int i = 0; i < count; i++)
            {
                var details = SteamMatchmakingServers.GetServerDetails(req, i);
                if (details.m_steamID.m_SteamID != 0 && details.m_nAppID == _appId.m_AppId)
                {
                    _serverItems.Add(details);

                    // Extra verbose line per server
                    Debug.Log($"[Browser]   ✔ {details.GetServerName()}  {details.m_nPlayers}/{details.m_nMaxPlayers}  {details.m_nPing} ms");
                }
            }

            _serverListView.RefreshItems();

            _isRefreshing = false;
            _lblStatus.text = $"{_serverItems.Count} server(s) found";
            _btnRefresh.SetEnabled(true);
            _serverListView.SetEnabled(true);

            if (req != HServerListRequest.Invalid)
                SteamMatchmakingServers.ReleaseRequest(req);
        }

        private void OnApplicationQuit()
        {
            if (_listHandle != HServerListRequest.Invalid)
                SteamMatchmakingServers.ReleaseRequest(_listHandle);
        }
    }
}
#endif
