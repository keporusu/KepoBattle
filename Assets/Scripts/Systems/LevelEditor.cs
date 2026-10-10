using System;
using System.Collections.Generic;
using System.Globalization;
using Components.Identity;
using Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems
{
    /// <summary>
    /// レベル作成システム
    /// 編集モードとプレイモードの切り替え、オブジェクト・地形ブロックの追加・選択・移動・削除、パラメータ編集を行う
    /// 編集は LevelManager.CurrentLevel(メモリ上のレベルデータ)に対して行い、生成済みのオブジェクトにも同じ変更を反映する
    /// StreamingAssets にはエディタからしか書き込めないため、Unity エディタ上でのみ動かす
    /// </summary>
    public class LevelEditor : MonoBehaviour
    {
        [SerializeField] private bool startInEditMode = false;

        //編集中のカメラ操作
        [SerializeField] private float minZoom = 2.0f;   //Orthographic Size の下限
        [SerializeField] private float maxZoom = 30.0f;  //Orthographic Size の上限
        [SerializeField] private float zoomStep = 0.1f;  //スクロール1回あたりの拡大率

        //ゲーム側から GUI 上にマウスがあるかを問い合わせるため、有効なものを1つ持つ
        private static LevelEditor _active;

        //GUI の配置
        private const float Margin = 10.0f;
        private const float PanelWidth = 220.0f;
        private const float InspectorWidth = 260.0f;
        private const float LabelWidth = 90.0f;
        private const int UtilityWindowId = 1;
        private const int PaletteWindowId = 2;
        private const int InspectorWindowId = 3;

        //クリックとドラッグを区別する距離(ピクセル)
        private const float DragThreshold = 4.0f;

        //グリッド線が多すぎる(ズームアウトしすぎ)場合は描かない
        private const int MaxGridLines = 200;

        private enum DragMode
        {
            None,
            MoveObjects,   //選択中のオブジェクトを動かす
            SelectRect,    //範囲選択
            ExtendTerrain, //選択中の地形ブロックを縦・横に増やす(Command + ドラッグ)
        }

        //編集の対象(オブジェクトモード / 地形モード)
        private enum EditTarget
        {
            Object,
            Terrain,
        }

        //増やす時に置くブロック1つ分
        private struct TerrainPlacement
        {
            public string Type;
            public Vector2 Position;
        }

        //GUI の領域(GUILayout.Window が中身に合わせて大きさを決める)
        private Rect _utilityRect = new Rect(Margin, Margin, PanelWidth, 0.0f);
        private Rect _paletteRect = new Rect(Margin, 0.0f, PanelWidth, 0.0f);
        private Rect _inspectorRect = new Rect(0.0f, Margin, InspectorWidth, 0.0f);

        //状態
        private readonly List<string> _selectedIds = new List<string>();
        private bool _snapToGrid = true;
        private EditTarget _editTarget = EditTarget.Object;

        //GUI のボタンから選択やオブジェクトの数を変えると、同じフレームの GUI の描画とずれてエラーになるため
        //構造を変える操作は次の Update で行う
        private readonly Queue<Action> _pendingActions = new Queue<Action>();

        //入力途中の文字列(数値として読めない "1." や "-" を保つ)
        private readonly Dictionary<string, string> _fieldBuffers = new Dictionary<string, string>();

        //ドラッグ
        private DragMode _dragMode = DragMode.None;
        private bool _hasDragged;
        private bool _isAdditive;
        private Vector2 _dragStartScreen;
        private Vector2 _dragStartWorld;
        private readonly Dictionary<string, Vector2> _dragStartPositions = new Dictionary<string, Vector2>();

        //地形ブロックを増やす
        private readonly List<TerrainPlacement> _extendSources = new List<TerrainPlacement>(); //増やす元(選択中のブロック)
        private readonly List<TerrainPlacement> _extendPreview = new List<TerrainPlacement>(); //離した時に置くブロック
        private Vector2Int _extendSizeInCells; //増やす元のまとまりの大きさ(マス数)

        //今の編集対象で扱う分類
        private LevelObjectCategory TargetCategory =>
            _editTarget == EditTarget.Terrain ? LevelObjectCategory.Terrain : LevelObjectCategory.Object;

        //カメラ
        private UnityEngine.Camera _camera;
        private float _defaultOrthographicSize;
        private bool _isPanning;
        private Vector2 _lastPanScreen;

        /// <summary>
        /// マウスがレベル作成システムの GUI の上にあるか？
        /// IMGUI は EventSystem で判定できないため、プレイ中にボタンを押して攻撃が出ないよう、ゲーム側からこれを見る
        /// </summary>
        public static bool IsPointerOverGui()
        {
            if (_active == null || !_active.enabled || Mouse.current == null) return false;
            return _active.IsOverGui(Mouse.current.position.ReadValue());
        }

        private void Awake()
        {
            //ビルドしたゲームでは無効にする
            if (!Application.isEditor)
            {
                enabled = false;
                return;
            }

            _active = this;
        }

        private void Start()
        {
            _camera = UnityEngine.Camera.main;
            if (_camera != null)
            {
                _defaultOrthographicSize = _camera.orthographicSize;
            }

            var levelManager = LevelManager.Instance;
            if (levelManager == null) return;

            levelManager.OnLevelBuilt += OnLevelBuilt;

            if (startInEditMode)
            {
                ChangeMode(true);
            }
        }

        private void OnDestroy()
        {
            if (_active == this)
            {
                _active = null;
            }

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnLevelBuilt -= OnLevelBuilt;
            }
        }

        private void OnLevelBuilt()
        {
            //作り直すと選択していたオブジェクトは無くなる
            _selectedIds.Clear();
            _fieldBuffers.Clear();
            _dragMode = DragMode.None;
            _extendPreview.Clear();
        }

        //****モード切り替え****

        private void ChangeMode(bool editMode)
        {
            var levelManager = LevelManager.Instance;
            if (levelManager == null) return;

            levelManager.SetEditMode(editMode);

            //プレイに戻る時はズームを元に戻す(位置はカメラの追従で戻る)
            if (!editMode && _camera != null)
            {
                _camera.orthographicSize = _defaultOrthographicSize;
            }
        }

        private void ChangeEditTarget(EditTarget editTarget)
        {
            if (_editTarget == editTarget) return;

            //オブジェクトと地形ブロックは同時に選択しない
            _editTarget = editTarget;
            _selectedIds.Clear();
            _dragMode = DragMode.None;
            _extendPreview.Clear();
        }

        //****入力****

        private void Update()
        {
            while (_pendingActions.Count > 0)
            {
                _pendingActions.Dequeue().Invoke();
            }

            var levelManager = LevelManager.Instance;
            if (levelManager == null || levelManager.CurrentLevel == null || !LevelManager.IsEditMode) return;

            if (_camera == null) _camera = UnityEngine.Camera.main;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (_camera == null || mouse == null || keyboard == null) return;

            //AutoSyncTransforms が無効なため、Transform を動かした後でも当たり判定の範囲が正しく取れるようにする
            Physics2D.SyncTransforms();

            //削除などで無くなったオブジェクトは選択から外す
            _selectedIds.RemoveAll(id => !levelManager.TryGetSpawnedObject(id, out _));

            HandleCamera(mouse);
            HandleMouse(levelManager, mouse, keyboard);
            HandleKeyboard(keyboard);
        }

        private void HandleCamera(Mouse mouse)
        {
            var screenPos = mouse.position.ReadValue();

            //右ドラッグ・中ドラッグで移動
            if ((mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame) && !IsOverGui(screenPos))
            {
                _isPanning = true;
                _lastPanScreen = screenPos;
            }

            if (_isPanning)
            {
                if (!mouse.rightButton.isPressed && !mouse.middleButton.isPressed)
                {
                    _isPanning = false;
                }
                else
                {
                    var unitsPerPixel = _camera.orthographicSize * 2.0f / Screen.height;
                    var delta = (screenPos - _lastPanScreen) * unitsPerPixel;
                    _camera.transform.position -= new Vector3(delta.x, delta.y, 0.0f);
                    _lastPanScreen = screenPos;
                }
            }

            //スクロールで拡大縮小
            var scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0.0f && !IsOverGui(screenPos))
            {
                var size = _camera.orthographicSize * Mathf.Pow(1.0f + zoomStep, -Mathf.Sign(scroll));
                _camera.orthographicSize = Mathf.Clamp(size, minZoom, maxZoom);
            }
        }

        private void HandleMouse(LevelManager levelManager, Mouse mouse, Keyboard keyboard)
        {
            var screenPos = mouse.position.ReadValue();
            var worldPos = ScreenToWorld(screenPos);

            if (mouse.leftButton.wasPressedThisFrame && !IsOverGui(screenPos))
            {
                //テキスト欄の入力中なら外す(外さないとキー入力がショートカットとして効かない)
                GUIUtility.keyboardControl = 0;

                _isAdditive = keyboard.shiftKey.isPressed;
                _hasDragged = false;
                _dragStartScreen = screenPos;
                _dragStartWorld = worldPos;

                //Command + ドラッグで地形ブロックを増やす(Windows では Ctrl)
                var isExtend = _editTarget == EditTarget.Terrain
                               && (keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed
                                   || keyboard.ctrlKey.isPressed);

                var picked = PickObject(levelManager, worldPos);
                if (picked != null && isExtend)
                {
                    //選択していないブロックから始めた場合は、そのブロックだけを増やす
                    if (!_selectedIds.Contains(picked.InstanceId))
                    {
                        _selectedIds.Clear();
                        Select(levelManager, picked.InstanceId);
                    }

                    BeginExtend(levelManager);
                }
                else if (picked != null)
                {
                    var id = picked.InstanceId;
                    if (_isAdditive && _selectedIds.Contains(id))
                    {
                        //Shift + クリックで選択から外す
                        _selectedIds.Remove(id);
                        _dragMode = DragMode.None;
                    }
                    else
                    {
                        if (!_isAdditive && !_selectedIds.Contains(id))
                        {
                            _selectedIds.Clear();
                        }

                        Select(levelManager, id);
                        BeginMove(levelManager);
                    }
                }
                else
                {
                    _dragMode = DragMode.SelectRect;
                }
            }

            if (_dragMode == DragMode.None) return;

            if (!_hasDragged && (screenPos - _dragStartScreen).magnitude >= DragThreshold)
            {
                _hasDragged = true;
            }

            switch (_dragMode)
            {
                case DragMode.MoveObjects:
                    if (mouse.leftButton.isPressed)
                    {
                        //クリックしただけで位置が吸着してずれないよう、ドラッグし始めてから動かす
                        if (_hasDragged) UpdateMove(levelManager, worldPos);
                    }
                    else
                    {
                        _dragMode = DragMode.None;
                    }
                    break;
                case DragMode.SelectRect:
                    if (!mouse.leftButton.isPressed)
                    {
                        FinishRectSelect(levelManager, worldPos);
                        _dragMode = DragMode.None;
                    }
                    break;
                case DragMode.ExtendTerrain:
                    if (mouse.leftButton.isPressed)
                    {
                        UpdateExtend(worldPos);
                    }
                    else
                    {
                        FinishExtend(levelManager);
                        _dragMode = DragMode.None;
                    }
                    break;
            }
        }

        private void HandleKeyboard(Keyboard keyboard)
        {
            //テキスト欄に入力中は、文字をショートカットとして扱わない
            if (GUIUtility.keyboardControl != 0) return;

            if (keyboard.dKey.wasPressedThisFrame || keyboard.deleteKey.wasPressedThisFrame)
            {
                DeleteSelection();
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                _selectedIds.Clear();
            }
        }

        //****選択・移動・削除****

        /// <summary>
        /// 今の編集対象で選択できるか？
        /// オブジェクトモードではオブジェクトのみ、地形モードでは地形ブロックのみ選択できる
        /// </summary>
        private bool IsSelectable(LevelManager levelManager, LevelObjectIdentity identity)
        {
            return levelManager.Registry.TryGetEntry(identity.TypeKey, out var entry)
                   && entry.category == TargetCategory;
        }

        /// <summary>
        /// 指定位置にあるオブジェクトを探す。重なっている場合は小さいものを優先する
        /// </summary>
        private LevelObjectIdentity PickObject(LevelManager levelManager, Vector2 worldPos)
        {
            LevelObjectIdentity picked = null;
            var pickedArea = float.PositiveInfinity;

            foreach (var identity in levelManager.SpawnedObjects)
            {
                if (!IsSelectable(levelManager, identity)) continue;

                var bounds = identity.GetBounds();
                if (worldPos.x < bounds.min.x || worldPos.x > bounds.max.x) continue;
                if (worldPos.y < bounds.min.y || worldPos.y > bounds.max.y) continue;

                var area = bounds.size.x * bounds.size.y;
                if (area < pickedArea)
                {
                    picked = identity;
                    pickedArea = area;
                }
            }

            return picked;
        }

        private void Select(LevelManager levelManager, string id)
        {
            if (_selectedIds.Contains(id)) return;
            _selectedIds.Add(id);

            //編集画面に全てのパラメータを並べられるよう、データに無いものを既定値で補う
            levelManager.FillMissingParameters(id);
        }

        private void BeginMove(LevelManager levelManager)
        {
            _dragMode = DragMode.MoveObjects;
            _dragStartPositions.Clear();

            foreach (var id in _selectedIds)
            {
                var objectData = levelManager.FindObjectData(id);
                if (objectData != null)
                {
                    _dragStartPositions[id] = objectData.position;
                }
            }
        }

        private void UpdateMove(LevelManager levelManager, Vector2 worldPos)
        {
            var delta = worldPos - _dragStartWorld;

            //地形ブロックはマス単位で動かし、並びを崩さない
            if (_editTarget == EditTarget.Terrain)
            {
                delta = new Vector2(
                    Mathf.Round(delta.x / LevelGrid.CellSize) * LevelGrid.CellSize,
                    Mathf.Round(delta.y / LevelGrid.CellSize) * LevelGrid.CellSize);
            }

            foreach (var pair in _dragStartPositions)
            {
                var target = SnapForTarget(pair.Value + delta);

                var objectData = levelManager.FindObjectData(pair.Key);
                if (objectData == null || objectData.position == target) continue;

                levelManager.MoveObject(pair.Key, target);
            }
        }

        private void FinishRectSelect(LevelManager levelManager, Vector2 worldPos)
        {
            if (!_isAdditive)
            {
                _selectedIds.Clear();
            }

            //ドラッグせずに何も無い所をクリックした場合は、選択を外すだけ
            if (!_hasDragged) return;

            var min = Vector2.Min(_dragStartWorld, worldPos);
            var max = Vector2.Max(_dragStartWorld, worldPos);

            //中心が範囲に入っているものを選ぶ
            foreach (var identity in levelManager.SpawnedObjects)
            {
                if (!IsSelectable(levelManager, identity)) continue;

                var center = identity.GetBounds().center;
                if (center.x < min.x || center.x > max.x) continue;
                if (center.y < min.y || center.y > max.y) continue;

                Select(levelManager, identity.InstanceId);
            }
        }

        /// <summary>
        /// 今の編集対象に合わせて位置を揃える
        /// 地形ブロックは常にマスの中心に揃え、オブジェクトは吸着が ON の時だけ 0.5 マス刻みに揃える
        /// </summary>
        private Vector2 SnapForTarget(Vector2 position)
        {
            if (_editTarget == EditTarget.Terrain) return LevelGrid.SnapTerrain(position);
            return _snapToGrid ? LevelGrid.SnapObject(position) : position;
        }

        //****地形ブロックを増やす****

        private void BeginExtend(LevelManager levelManager)
        {
            _dragMode = DragMode.ExtendTerrain;
            _extendSources.Clear();
            _extendPreview.Clear();

            var minCell = new Vector2Int(int.MaxValue, int.MaxValue);
            var maxCell = new Vector2Int(int.MinValue, int.MinValue);

            foreach (var id in _selectedIds)
            {
                var objectData = levelManager.FindObjectData(id);
                if (objectData == null) continue;

                var position = LevelGrid.SnapTerrain(objectData.position);
                _extendSources.Add(new TerrainPlacement { Type = objectData.type, Position = position });

                var cell = ToCell(position);
                minCell = Vector2Int.Min(minCell, cell);
                maxCell = Vector2Int.Max(maxCell, cell);
            }

            //選択中のブロックを1つのまとまりとし、その大きさ単位で繰り返す
            _extendSizeInCells = _extendSources.Count > 0 ? maxCell - minCell + Vector2Int.one : Vector2Int.one;
        }

        private void UpdateExtend(Vector2 worldPos)
        {
            _extendPreview.Clear();

            var delta = worldPos - _dragStartWorld;

            //縦・横のうち大きく動かした方向にだけ増やす
            var isHorizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            var distance = isHorizontal ? delta.x : delta.y;
            var step = (isHorizontal ? _extendSizeInCells.x : _extendSizeInCells.y) * LevelGrid.CellSize;

            //まとまりの半分以上動かしたら1つ増やす
            var count = Mathf.FloorToInt(Mathf.Abs(distance) / step + 0.5f);
            if (count == 0) return;

            var direction = (isHorizontal ? Vector2.right : Vector2.up) * Mathf.Sign(distance);
            for (var i = 1; i <= count; i++)
            {
                var offset = direction * step * i;
                foreach (var source in _extendSources)
                {
                    _extendPreview.Add(new TerrainPlacement { Type = source.Type, Position = source.Position + offset });
                }
            }
        }

        private void FinishExtend(LevelManager levelManager)
        {
            //既にブロックがあるマスには置かない
            var occupied = new HashSet<Vector2Int>();
            foreach (var identity in levelManager.SpawnedObjects)
            {
                if (!IsSelectable(levelManager, identity)) continue;

                var objectData = levelManager.FindObjectData(identity.InstanceId);
                if (objectData != null)
                {
                    occupied.Add(ToCell(LevelGrid.SnapTerrain(objectData.position)));
                }
            }

            foreach (var placement in _extendPreview)
            {
                if (!occupied.Add(ToCell(placement.Position))) continue;

                //増やしたブロックも選択に加え、続けて増やせるようにする
                var spawned = levelManager.AddObject(placement.Type, placement.Position);
                if (spawned != null)
                {
                    Select(levelManager, spawned.InstanceId);
                }
            }

            _extendPreview.Clear();
        }

        private static Vector2Int ToCell(Vector2 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / LevelGrid.CellSize),
                Mathf.FloorToInt(position.y / LevelGrid.CellSize));
        }

        private void DeleteSelection()
        {
            var levelManager = LevelManager.Instance;
            if (levelManager == null) return;

            foreach (var id in _selectedIds)
            {
                levelManager.RemoveObject(id);
            }

            _selectedIds.Clear();
            _dragMode = DragMode.None;
        }

        private void AddObject(string type)
        {
            var levelManager = LevelManager.Instance;
            if (levelManager == null || _camera == null) return;

            //画面の中央に置く
            Vector2 position = SnapForTarget(_camera.transform.position);

            var spawned = levelManager.AddObject(type, position);
            if (spawned == null) return;

            _selectedIds.Clear();
            Select(levelManager, spawned.InstanceId);
        }

        //****GUI****

        private void OnGUI()
        {
            var levelManager = LevelManager.Instance;
            if (levelManager == null) return;

            var isEditing = LevelManager.IsEditMode && levelManager.CurrentLevel != null;

            //ワールド上の表示(グリッド・選択枠・範囲選択の枠)を先に描き、ウィンドウを上に重ねる
            if (isEditing && _camera != null && Event.current.type == EventType.Repaint)
            {
                DrawGrid();
                DrawSelection(levelManager);
                DrawExtendPreview();
                DrawSelectRect();
            }

            _utilityRect = GUILayout.Window(UtilityWindowId, _utilityRect, DrawUtilityWindow, "レベル作成",
                GUILayout.Width(PanelWidth));

            if (!isEditing) return;

            _paletteRect.y = _utilityRect.yMax + Margin;
            var paletteTitle = _editTarget == EditTarget.Terrain ? "地形" : "オブジェクト";
            _paletteRect = GUILayout.Window(PaletteWindowId, _paletteRect, DrawPaletteWindow, paletteTitle,
                GUILayout.Width(PanelWidth));

            if (_selectedIds.Count > 0)
            {
                _inspectorRect.x = Screen.width - InspectorWidth - Margin;
                _inspectorRect = GUILayout.Window(InspectorWindowId, _inspectorRect, DrawInspectorWindow, "パラメータ",
                    GUILayout.Width(InspectorWidth));
            }
        }

        private void DrawUtilityWindow(int windowId)
        {
            var levelManager = LevelManager.Instance;
            var isEditMode = LevelManager.IsEditMode;

            var modeText = isEditMode ? "モード: 編集" : "モード: プレイ";
            if (levelManager != null && levelManager.IsDirty)
            {
                modeText += "（未保存）";
            }
            GUILayout.Label(modeText);

            GUILayout.BeginHorizontal();

            //今のモードのボタンは押せないようにする
            GUI.enabled = !isEditMode;
            if (GUILayout.Button("編集"))
            {
                _pendingActions.Enqueue(() => ChangeMode(true));
            }

            GUI.enabled = isEditMode;
            if (GUILayout.Button("再生"))
            {
                _pendingActions.Enqueue(() => ChangeMode(false));
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (!isEditMode) return;

            //編集対象(オブジェクトモード / 地形モード)
            GUILayout.BeginHorizontal();
            GUI.enabled = _editTarget != EditTarget.Object;
            if (GUILayout.Button("オブジェクト"))
            {
                _pendingActions.Enqueue(() => ChangeEditTarget(EditTarget.Object));
            }

            GUI.enabled = _editTarget != EditTarget.Terrain;
            if (GUILayout.Button("地形"))
            {
                _pendingActions.Enqueue(() => ChangeEditTarget(EditTarget.Terrain));
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (_editTarget == EditTarget.Object)
            {
                _snapToGrid = GUILayout.Toggle(_snapToGrid, "グリッドに吸着");
            }
            else
            {
                GUILayout.Label("Command + ドラッグで増やす");
            }
        }

        private void DrawPaletteWindow(int windowId)
        {
            var levelManager = LevelManager.Instance;
            if (levelManager == null || levelManager.Registry == null) return;

            foreach (var entry in levelManager.Registry.Entries)
            {
                if (entry.category != TargetCategory) continue;

                if (GUILayout.Button(entry.key))
                {
                    var type = entry.key;
                    _pendingActions.Enqueue(() => AddObject(type));
                }
            }
        }

        private void DrawInspectorWindow(int windowId)
        {
            var levelManager = LevelManager.Instance;
            if (levelManager == null) return;

            if (_selectedIds.Count > 1)
            {
                GUILayout.Label($"{_selectedIds.Count}個を選択中");
                GUILayout.Label("ドラッグで移動 / D キーで削除");
            }
            else
            {
                DrawSingleInspector(levelManager, _selectedIds[0]);
            }

            if (GUILayout.Button("削除"))
            {
                _pendingActions.Enqueue(DeleteSelection);
            }
        }

        private void DrawSingleInspector(LevelManager levelManager, string id)
        {
            var objectData = levelManager.FindObjectData(id);
            if (objectData == null) return;

            GUILayout.Label($"種別: {objectData.type}");

            //位置(入力した値をそのまま使い、吸着はしない)
            var position = objectData.position;
            var x = FloatField("位置 X", $"{id}/position.x", position.x);
            var y = FloatField("位置 Y", $"{id}/position.y", position.y);
            if (x != position.x || y != position.y)
            {
                levelManager.MoveObject(id, new Vector2(x, y));
            }

            //固有パラメータ
            //書き換えると entries の要素が置き換わるため、写しを回す
            var parameters = objectData.parameters;
            var changed = false;
            foreach (var entry in new List<LevelParameter>(parameters.Entries))
            {
                var controlName = $"{id}/{entry.key}";
                switch (entry.type)
                {
                    case LevelParameterType.Bool:
                    {
                        parameters.TryGetBool(entry.key, out var value);
                        var edited = GUILayout.Toggle(value, entry.key);
                        if (edited != value)
                        {
                            parameters.SetBool(entry.key, edited);
                            changed = true;
                        }
                        break;
                    }
                    case LevelParameterType.Float:
                    {
                        parameters.TryGetFloat(entry.key, out var value);
                        var edited = FloatField(entry.key, controlName, value);
                        if (edited != value)
                        {
                            parameters.SetFloat(entry.key, edited);
                            changed = true;
                        }
                        break;
                    }
                    case LevelParameterType.Int:
                    {
                        parameters.TryGetInt(entry.key, out var value);
                        var edited = IntField(entry.key, controlName, value);
                        if (edited != value)
                        {
                            parameters.SetInt(entry.key, edited);
                            changed = true;
                        }
                        break;
                    }
                }
            }

            if (changed)
            {
                levelManager.ApplyParameters(id);
            }
        }

        private float FloatField(string label, string controlName, float value)
        {
            var text = BufferedTextField(label, controlName, value.ToString("0.###", CultureInfo.InvariantCulture));
            if (text != null && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return value;
        }

        private int IntField(string label, string controlName, int value)
        {
            var text = BufferedTextField(label, controlName, value.ToString(CultureInfo.InvariantCulture));
            if (text != null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return value;
        }

        /// <summary>
        /// 入力途中の文字列を保つテキスト欄
        /// </summary>
        /// <returns>この描画で書き換えられた文字列。書き換えが無ければ null</returns>
        private string BufferedTextField(string label, string controlName, string valueText)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(LabelWidth));

            var isFocused = GUI.GetNameOfFocusedControl() == controlName;
            if (!isFocused)
            {
                _fieldBuffers.Remove(controlName);
            }

            var shown = isFocused && _fieldBuffers.TryGetValue(controlName, out var buffered) ? buffered : valueText;

            GUI.SetNextControlName(controlName);
            var edited = GUILayout.TextField(shown);
            GUILayout.EndHorizontal();

            if (edited == shown) return null;

            _fieldBuffers[controlName] = edited;
            return edited;
        }

        //****ワールド上の表示****

        private void DrawGrid()
        {
            var bottomLeft = ScreenToWorld(Vector2.zero);
            var topRight = ScreenToWorld(new Vector2(Screen.width, Screen.height));

            var cell = LevelGrid.CellSize;
            var startX = Mathf.Floor(bottomLeft.x / cell) * cell;
            var startY = Mathf.Floor(bottomLeft.y / cell) * cell;
            var countX = Mathf.CeilToInt((topRight.x - startX) / cell);
            var countY = Mathf.CeilToInt((topRight.y - startY) / cell);
            if (countX + countY > MaxGridLines) return;

            var color = new Color(1.0f, 1.0f, 1.0f, 0.15f);

            //マスの辺(整数の線)に線を引く
            for (var i = 0; i <= countX; i++)
            {
                var guiX = WorldToGui(new Vector2(startX + i * cell, 0.0f)).x;
                DrawRect(new Rect(guiX, 0.0f, 1.0f, Screen.height), color);
            }

            for (var i = 0; i <= countY; i++)
            {
                var guiY = WorldToGui(new Vector2(0.0f, startY + i * cell)).y;
                DrawRect(new Rect(0.0f, guiY, Screen.width, 1.0f), color);
            }
        }

        private void DrawSelection(LevelManager levelManager)
        {
            var color = new Color(1.0f, 0.85f, 0.0f, 1.0f);
            foreach (var id in _selectedIds)
            {
                if (!levelManager.TryGetSpawnedObject(id, out var identity)) continue;

                var bounds = identity.GetBounds();
                var guiMin = WorldToGui(new Vector2(bounds.min.x, bounds.max.y));
                var guiMax = WorldToGui(new Vector2(bounds.max.x, bounds.min.y));
                DrawOutline(Rect.MinMaxRect(guiMin.x, guiMin.y, guiMax.x, guiMax.y), color, 2.0f);
            }
        }

        private void DrawExtendPreview()
        {
            if (_dragMode != DragMode.ExtendTerrain) return;

            var half = LevelGrid.CellSize * 0.5f;
            var fill = new Color(0.4f, 1.0f, 0.4f, 0.2f);
            var outline = new Color(0.4f, 1.0f, 0.4f, 0.9f);

            foreach (var placement in _extendPreview)
            {
                var guiMin = WorldToGui(placement.Position + new Vector2(-half, half));
                var guiMax = WorldToGui(placement.Position + new Vector2(half, -half));
                var rect = Rect.MinMaxRect(guiMin.x, guiMin.y, guiMax.x, guiMax.y);
                DrawRect(rect, fill);
                DrawOutline(rect, outline, 1.0f);
            }
        }

        private void DrawSelectRect()
        {
            if (_dragMode != DragMode.SelectRect || !_hasDragged || Mouse.current == null) return;

            var start = ScreenToGui(_dragStartScreen);
            var end = ScreenToGui(Mouse.current.position.ReadValue());
            var rect = Rect.MinMaxRect(
                Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y),
                Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));

            DrawRect(rect, new Color(0.3f, 0.6f, 1.0f, 0.15f));
            DrawOutline(rect, new Color(0.3f, 0.6f, 1.0f, 0.8f), 1.0f);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawOutline(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        }

        //****座標変換****
        //スクリーン座標は左下原点、GUI 座標は左上原点

        private bool IsOverGui(Vector2 screenPos)
        {
            var guiPos = ScreenToGui(screenPos);
            if (_utilityRect.Contains(guiPos)) return true;

            //編集中のみ表示するウィンドウ
            if (!LevelManager.IsEditMode) return false;
            if (_paletteRect.Contains(guiPos)) return true;
            return _selectedIds.Count > 0 && _inspectorRect.Contains(guiPos);
        }

        private Vector2 ScreenToWorld(Vector2 screenPos)
        {
            return _camera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0.0f));
        }

        private Vector2 WorldToGui(Vector2 worldPos)
        {
            Vector2 screenPos = _camera.WorldToScreenPoint(worldPos);
            return ScreenToGui(screenPos);
        }

        private static Vector2 ScreenToGui(Vector2 screenPos)
        {
            return new Vector2(screenPos.x, Screen.height - screenPos.y);
        }
    }
}
