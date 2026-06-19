using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Client.Listeners;
using ReactiveUI;

namespace Client.Sessions;

internal partial class SessionsGraphView : UserControl
{
    public event EventHandler<BeaconSessionViewModel>? SessionActivated;

    private const double NodeWidth = 164;
    private const double NodeHeight = 60;
    private const double ListenerNodeWidth = 148;
    private const double ListenerNodeHeight = 46;
    private const double HGap = 48;
    private const double VGap = 72;
    private const double StartX = 32;
    private const double StartY = 32;

    private Canvas? _edgeCanvas;
    private Canvas? _nodeCanvas;
    private SessionsPanelViewModel? _vm;

    private readonly Dictionary<uint, GraphNode> _nodes = new();
    private readonly Dictionary<string, ListenerNode> _listenerNodes = new();
    private readonly List<(string ListenerName, uint BeaconId, Line Line)> _listenerEdges = new();
    private readonly List<(uint ParentId, uint ChildId, Line Line)> _beaconEdges = new();

    // unified drag state — Tag on Border distinguishes type (uint = beacon, string = listener)
    private Border? _draggingElement;
    private Point _dragOffset;

    private sealed class GraphNode(BeaconSessionViewModel session, Border element)
    {
        public BeaconSessionViewModel Session { get; } = session;
        public Border Element { get; } = element;
        public double X { get; set; }
        public double Y { get; set; }
        public Point Center => new(X + NodeWidth / 2, Y + NodeHeight / 2);
    }

    private sealed class ListenerNode(ListenerItemViewModel listener, Border element)
    {
        public ListenerItemViewModel Listener { get; } = listener;
        public Border Element { get; } = element;
        public double X { get; set; }
        public double Y { get; set; }
        public Point Center => new(X + ListenerNodeWidth / 2, Y + ListenerNodeHeight / 2);
    }

    public SessionsGraphView()
    {
        InitializeComponent();
        _edgeCanvas = this.FindControl<Canvas>("PART_EdgeCanvas");
        _nodeCanvas = this.FindControl<Canvas>("PART_NodeCanvas");
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm is not null)
        {
            _vm.Sessions.CollectionChanged -= OnSessionsChanged;
            _vm.Listeners.CollectionChanged -= OnListenersChanged;
            _vm = null;
        }

        if (DataContext is not SessionsPanelViewModel vm) return;
        _vm = vm;
        _vm.Sessions.CollectionChanged += OnSessionsChanged;
        _vm.Listeners.CollectionChanged += OnListenersChanged;
        RebuildAll();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && change.NewValue is true)
            RebuildAll();
    }

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems is not null)
                        foreach (BeaconSessionViewModel s in e.NewItems)
                            AddBeaconNode(s);
                    LayoutAll();
                    RedrawEdges();
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems is not null)
                        foreach (BeaconSessionViewModel s in e.OldItems)
                            RemoveBeaconNode(s.BeaconId);
                    LayoutAll();
                    RedrawEdges();
                    break;

                case NotifyCollectionChangedAction.Reset:
                    ClearAll();
                    break;
            }
        });
    }

    private void OnListenersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems is not null)
                        foreach (ListenerItemViewModel l in e.NewItems)
                            AddListenerNode(l);
                    LayoutAll();
                    RedrawEdges();
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems is not null)
                        foreach (ListenerItemViewModel l in e.OldItems)
                            RemoveListenerNode(l.Name);
                    LayoutAll();
                    RedrawEdges();
                    break;

                case NotifyCollectionChangedAction.Reset:
                    ClearAll();
                    break;
            }
        });
    }

    private void RebuildAll()
    {
        if (_edgeCanvas is null || _nodeCanvas is null || _vm is null) return;
        ClearAll();
        foreach (var listener in _vm.Listeners)
            AddListenerNode(listener);
        foreach (var session in _vm.Sessions)
            AddBeaconNode(session);
        LayoutAll();
        RedrawEdges();
    }

    private void ClearAll()
    {
        _edgeCanvas?.Children.Clear();
        _nodeCanvas?.Children.Clear();
        _nodes.Clear();
        _listenerNodes.Clear();
        _listenerEdges.Clear();
        _beaconEdges.Clear();
        _draggingElement = null;
    }

    // ── Listener nodes ────────────────────────────────────────────────────────

    private void AddListenerNode(ListenerItemViewModel listener)
    {
        if (_nodeCanvas is null || _listenerNodes.ContainsKey(listener.Name)) return;
        var element = BuildListenerElement(listener);
        var node = new ListenerNode(listener, element);
        _listenerNodes[listener.Name] = node;
        _nodeCanvas.Children.Add(element);
    }

    private void RemoveListenerNode(string name)
    {
        if (_nodeCanvas is null || !_listenerNodes.Remove(name, out var node)) return;
        _nodeCanvas.Children.Remove(node.Element);
    }

    private Border BuildListenerElement(ListenerItemViewModel listener)
    {
        var nameText = new TextBlock
        {
            Text = listener.Name,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#E6EDF3")),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = ListenerNodeWidth - 30
        };

        var portText = new TextBlock
        {
            Text = listener.PortDisplay,
            FontSize = 10,
            FontFamily = new FontFamily("Consolas,monospace"),
            Foreground = new SolidColorBrush(Color.Parse("#A78BFA")),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = "▸",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.Parse("#7C3AED")),
                    VerticalAlignment = VerticalAlignment.Center
                },
                nameText
            }
        };

        var content = new StackPanel
        {
            Spacing = 3,
            Children = { titleRow, portText }
        };

        var border = new Border
        {
            Width = ListenerNodeWidth,
            Height = ListenerNodeHeight,
            Background = new SolidColorBrush(Color.Parse("#1A0F2E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#7C3AED")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8),
            Child = content,
            Cursor = new Cursor(StandardCursorType.SizeAll),
            Tag = listener.Name
        };

        border.PointerPressed += OnPointerPressed;
        border.PointerMoved += OnPointerMoved;
        border.PointerReleased += OnPointerReleased;

        return border;
    }

    // ── Beacon nodes ──────────────────────────────────────────────────────────

    private void AddBeaconNode(BeaconSessionViewModel session)
    {
        if (_nodeCanvas is null || _nodes.ContainsKey(session.BeaconId)) return;
        var element = BuildBeaconElement(session);
        var node = new GraphNode(session, element);
        _nodes[session.BeaconId] = node;
        _nodeCanvas.Children.Add(element);
    }

    private void RemoveBeaconNode(uint beaconId)
    {
        if (_nodeCanvas is null || !_nodes.Remove(beaconId, out var node)) return;
        _nodeCanvas.Children.Remove(node.Element);
    }

    private Border BuildBeaconElement(BeaconSessionViewModel session)
    {
        var healthDot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = session.HealthBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        var computerText = new TextBlock
        {
            Text = session.Computer,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#E6EDF3")),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = NodeWidth - 40
        };

        var userText = new TextBlock
        {
            Text = session.UserDisplay,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#8B949E")),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = NodeWidth - 20
        };

        var processText = new TextBlock
        {
            Text = $"{session.Process}:{session.Pid}",
            FontSize = 10,
            FontFamily = new FontFamily("Consolas,monospace"),
            Foreground = new SolidColorBrush(Color.Parse("#6E7681")),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = NodeWidth - 20
        };

        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { healthDot, computerText }
        };

        var content = new StackPanel
        {
            Spacing = 3,
            Children = { titleRow, userText, processText }
        };

        var border = new Border
        {
            Width = NodeWidth,
            Height = NodeHeight,
            Background = new SolidColorBrush(Color.Parse("#161B22")),
            BorderBrush = session.HealthBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 9),
            Child = content,
            Cursor = new Cursor(StandardCursorType.SizeAll),
            Tag = session.BeaconId
        };

        session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BeaconSessionViewModel.HealthBrush))
            {
                healthDot.Fill = session.HealthBrush;
                border.BorderBrush = session.HealthBrush;
            }
        };

        var interactItem = new MenuItem { Header = "Interact" };
        interactItem.Click += (_, _) => SessionActivated?.Invoke(this, session);

        border.ContextMenu = new ContextMenu
        {
            Items =
            {
                interactItem,
                new MenuItem { Header = "Delete Session", Command = session.DeleteCommand }
            }
        };

        border.PointerPressed += OnPointerPressed;
        border.PointerMoved += OnPointerMoved;
        border.PointerReleased += OnPointerReleased;
        border.DoubleTapped += OnBeaconDoubleTapped;

        return border;
    }

    // ── Drag ──────────────────────────────────────────────────────────────────

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border) return;
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

        _draggingElement = border;
        _dragOffset = e.GetPosition(_nodeCanvas) - new Point(
            Canvas.GetLeft(border), Canvas.GetTop(border));
        e.Pointer.Capture(border);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggingElement is null || _nodeCanvas is null) return;

        var pos = e.GetPosition(_nodeCanvas);
        double newX = Math.Max(0, pos.X - _dragOffset.X);
        double newY = Math.Max(0, pos.Y - _dragOffset.Y);

        Canvas.SetLeft(_draggingElement, newX);
        Canvas.SetTop(_draggingElement, newY);

        // keep data-model positions in sync so edges follow
        if (_draggingElement.Tag is uint beaconId && _nodes.TryGetValue(beaconId, out var bNode))
        {
            bNode.X = newX;
            bNode.Y = newY;
        }
        else if (_draggingElement.Tag is string listenerName &&
                 _listenerNodes.TryGetValue(listenerName, out var lNode))
        {
            lNode.X = newX;
            lNode.Y = newY;
        }

        UpdateEdgePositions();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggingElement is null) return;
        e.Pointer.Capture(null);
        _draggingElement = null;
    }

    private void OnBeaconDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.Tag is uint beaconId && _nodes.TryGetValue(beaconId, out var node))
            SessionActivated?.Invoke(this, node.Session);
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private void LayoutAll()
    {
        if (_nodeCanvas is null) return;
        if (_nodes.Count == 0 && _listenerNodes.Count == 0) return;

        // Build beacon parent→children map
        var childrenOf = new Dictionary<uint, List<uint>>();
        var hasParentInGraph = new HashSet<uint>();

        foreach (var (id, node) in _nodes)
        {
            if (node.Session.ParentId.HasValue &&
                _nodes.ContainsKey(node.Session.ParentId.Value))
            {
                var pid = node.Session.ParentId.Value;
                if (!childrenOf.TryGetValue(pid, out var list))
                    childrenOf[pid] = list = [];
                list.Add(id);
                hasParentInGraph.Add(id);
            }
        }

        var rootBeaconIds = _nodes.Keys
            .Where(id => !hasParentInGraph.Contains(id))
            .OrderBy(id => id)
            .ToList();

        // Group root beacons by listener name
        var rootsByListener = rootBeaconIds
            .GroupBy(id => _nodes[id].Session.Listener)
            .ToDictionary(g => g.Key, g => g.ToList());

        double beaconRowY = _listenerNodes.Count > 0
            ? StartY + ListenerNodeHeight + VGap
            : StartY;

        double x = StartX;

        // Layout each listener with its beacon subtrees underneath
        foreach (var (listenerName, listenerNode) in _listenerNodes.OrderBy(p => p.Key))
        {
            var rootsForThisListener = rootsByListener.TryGetValue(listenerName, out var roots)
                ? roots
                : [];

            double colStart = x;
            double bx = x;

            foreach (var beaconId in rootsForThisListener)
            {
                double w = PlaceSubtree(beaconId, bx, beaconRowY, childrenOf);
                bx += w + HGap;
            }

            double subtreeWidth = bx - x - (rootsForThisListener.Count > 0 ? HGap : 0);
            double colWidth = Math.Max(ListenerNodeWidth, subtreeWidth);

            listenerNode.X = colStart + (colWidth - ListenerNodeWidth) / 2;
            listenerNode.Y = StartY;

            Canvas.SetLeft(listenerNode.Element, listenerNode.X);
            Canvas.SetTop(listenerNode.Element, listenerNode.Y);

            x = colStart + colWidth + HGap;
        }

        // Orphan root beacons (listener not in current listener list)
        var knownListeners = _listenerNodes.Keys.ToHashSet();
        var orphans = rootBeaconIds
            .Where(id => !knownListeners.Contains(_nodes[id].Session.Listener))
            .ToList();

        foreach (var beaconId in orphans)
        {
            double w = PlaceSubtree(beaconId, x, beaconRowY, childrenOf);
            x += w + HGap;
        }

        // Apply final canvas positions for all beacon nodes
        foreach (var node in _nodes.Values)
        {
            Canvas.SetLeft(node.Element, node.X);
            Canvas.SetTop(node.Element, node.Y);
        }
    }

    private double PlaceSubtree(uint id, double x, double y,
        Dictionary<uint, List<uint>> childrenOf)
    {
        var node = _nodes[id];

        if (!childrenOf.TryGetValue(id, out var children) || children.Count == 0)
        {
            node.X = x;
            node.Y = y;
            return NodeWidth;
        }

        double cx = x;
        double totalW = 0;
        foreach (var childId in children)
        {
            double w = PlaceSubtree(childId, cx, y + NodeHeight + VGap, childrenOf);
            cx += w + HGap;
            totalW += w + HGap;
        }
        totalW = Math.Max(totalW - HGap, NodeWidth);

        node.X = x + (totalW - NodeWidth) / 2;
        node.Y = y;
        return totalW;
    }

    // ── Edges ─────────────────────────────────────────────────────────────────

    private void RedrawEdges()
    {
        if (_edgeCanvas is null) return;
        _edgeCanvas.Children.Clear();
        _listenerEdges.Clear();
        _beaconEdges.Clear();

        // Build set of beacon IDs that have a parent in the graph
        var hasParentInGraph = _nodes.Values
            .Where(n => n.Session.ParentId.HasValue &&
                        _nodes.ContainsKey(n.Session.ParentId.Value))
            .Select(n => n.Session.BeaconId)
            .ToHashSet();

        // Listener → root beacon edges (purple)
        foreach (var (beaconId, node) in _nodes)
        {
            if (hasParentInGraph.Contains(beaconId)) continue;
            if (!_listenerNodes.TryGetValue(node.Session.Listener, out var listenerNode)) continue;

            var line = MakeLine(listenerNode.Center, node.Center, "#7C3AED", 1.5);
            _listenerEdges.Add((node.Session.Listener, beaconId, line));
            _edgeCanvas.Children.Add(line);
        }

        // Beacon → parent beacon edges (dim)
        foreach (var (beaconId, node) in _nodes)
        {
            if (!node.Session.ParentId.HasValue) continue;
            if (!_nodes.TryGetValue(node.Session.ParentId.Value, out var parent)) continue;

            var line = MakeLine(parent.Center, node.Center, "#30363D", 1.5);
            _beaconEdges.Add((node.Session.ParentId.Value, beaconId, line));
            _edgeCanvas.Children.Add(line);
        }
    }

    private static Line MakeLine(Point start, Point end, string hex, double thickness) => new()
    {
        StartPoint = start,
        EndPoint = end,
        Stroke = new SolidColorBrush(Color.Parse(hex)),
        StrokeThickness = thickness
    };

    private void UpdateEdgePositions()
    {
        foreach (var (listenerName, beaconId, line) in _listenerEdges)
        {
            if (!_listenerNodes.TryGetValue(listenerName, out var lNode)) continue;
            if (!_nodes.TryGetValue(beaconId, out var bNode)) continue;
            line.StartPoint = lNode.Center;
            line.EndPoint = bNode.Center;
        }

        foreach (var (parentId, childId, line) in _beaconEdges)
        {
            if (!_nodes.TryGetValue(parentId, out var parent)) continue;
            if (!_nodes.TryGetValue(childId, out var child)) continue;
            line.StartPoint = parent.Center;
            line.EndPoint = child.Center;
        }
    }
}
