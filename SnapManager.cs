using System;
using System.Windows;

namespace NumLockIndicator;

public class SnapManager
{
    private readonly IndicatorWindow _windowA;
    private readonly IndicatorWindow _windowB;
    private readonly double _threshold;
    private readonly double _gap;

    private bool _updating;
    private bool _isSnapped;
    private IndicatorWindow? _leader;
    private IndicatorWindow? _follower;
    private bool _isVertical;

    private bool _groupDragging;
    private double _leaderStartLeft, _leaderStartTop;
    private double _followerStartLeft, _followerStartTop;
    private bool _detached;

    public SnapManager(IndicatorWindow windowA, IndicatorWindow windowB, double threshold = 10, double gap = 2)
    {
        _windowA = windowA;
        _windowB = windowB;
        _threshold = threshold;
        _gap = gap;

        _windowA.DragStarted += OnDragStarted;
        _windowB.DragStarted += OnDragStarted;
        _windowA.DragCompleted += OnDragCompleted;
        _windowB.DragCompleted += OnDragCompleted;
        _windowA.PositionChanged += OnPositionChanged;
        _windowB.PositionChanged += OnPositionChanged;
    }

    private void OnDragStarted(IndicatorWindow window)
    {
        if (_isSnapped && _leader != null && _follower != null)
        {
            if (window == _leader)
            {
                _groupDragging = true;
                _leaderStartLeft = _leader.Left;
                _leaderStartTop = _leader.Top;
                _followerStartLeft = _follower.Left;
                _followerStartTop = _follower.Top;
            }
            else
            {
                _isSnapped = false;
                _groupDragging = false;
                _detached = true;
                _leader = null;
                _follower = null;
            }
        }
    }

    private void OnDragCompleted(IndicatorWindow window)
    {
        _groupDragging = false;
        _detached = false;
        TrySnap(window);
    }

    private void OnPositionChanged(IndicatorWindow moved)
    {
        if (_updating) return;

        if (_groupDragging && _leader != null && _follower != null && moved == _leader)
        {
            _updating = true;
            try
            {
                double dx = _leader.Left - _leaderStartLeft;
                double dy = _leader.Top - _leaderStartTop;
                _follower.Left = _followerStartLeft + dx;
                _follower.Top = _followerStartTop + dy;
                ClampWindow(_follower);
            }
            finally
            {
                _updating = false;
            }
            return;
        }

        if (_detached) return;

        TrySnap(moved);
    }

    private void TrySnap(IndicatorWindow moved)
    {
        if (_updating) return;
        _updating = true;

        try
        {
            var other = moved == _windowA ? _windowB : _windowA;
            if (!other.IsVisible || !moved.IsVisible) return;

            var mV = moved.GetVisualRect();
            var oV = other.GetVisualRect();

            double bestDist = double.MaxValue;
            double targetLeft = moved.Left;
            double targetTop = moved.Top;
            bool bestIsVertical = true;
            bool movedIsLeader = false;

            double m = IndicatorWindow.VisualMargin;

            // moved below other → moved is follower (bottom)
            double d1 = Math.Abs(mV.Top - oV.Bottom);
            if (d1 < _threshold)
            {
                double tTop = oV.Bottom + _gap - m;
                if (d1 < bestDist)
                {
                    bestDist = d1;
                    targetLeft = other.Left;
                    targetTop = tTop;
                    bestIsVertical = true;
                    movedIsLeader = false;
                }
            }

            // moved above other → moved is leader (top)
            double d2 = Math.Abs(mV.Bottom - oV.Top);
            if (d2 < _threshold)
            {
                double tTop = oV.Top - _gap - mV.Height - m;
                if (d2 < bestDist)
                {
                    bestDist = d2;
                    targetLeft = other.Left;
                    targetTop = tTop;
                    bestIsVertical = true;
                    movedIsLeader = true;
                }
            }

            // moved right of other → moved is follower (right)
            double d3 = Math.Abs(mV.Left - oV.Right);
            if (d3 < _threshold)
            {
                double tLeft = oV.Right + _gap - m;
                if (d3 < bestDist)
                {
                    bestDist = d3;
                    targetLeft = tLeft;
                    targetTop = other.Top;
                    bestIsVertical = false;
                    movedIsLeader = false;
                }
            }

            // moved left of other → moved is leader (left)
            double d4 = Math.Abs(mV.Right - oV.Left);
            if (d4 < _threshold)
            {
                double tLeft = oV.Left - _gap - mV.Width - m;
                if (d4 < bestDist)
                {
                    bestDist = d4;
                    targetLeft = tLeft;
                    targetTop = other.Top;
                    bestIsVertical = false;
                    movedIsLeader = true;
                }
            }

            if (bestDist < double.MaxValue)
            {
                moved.Left = targetLeft;
                moved.Top = targetTop;

                _isSnapped = true;
                _isVertical = bestIsVertical;
                if (movedIsLeader)
                {
                    _leader = moved;
                    _follower = other;
                }
                else
                {
                    _leader = other;
                    _follower = moved;
                }
            }
            else
            {
                _isSnapped = false;
                _leader = null;
                _follower = null;
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private static void ClampWindow(IndicatorWindow w)
    {
        double sw = System.Windows.SystemParameters.PrimaryScreenWidth;
        double sh = System.Windows.SystemParameters.PrimaryScreenHeight;
        double ww = w.ActualWidth > 0 ? w.ActualWidth : w.Width;
        double wh = w.ActualHeight > 0 ? w.ActualHeight : w.Height;
        double min = 20;

        if (w.Left + ww - min < 0) w.Left = min - ww;
        if (w.Top + wh - min < 0) w.Top = min - wh;
        if (w.Left > sw - min) w.Left = sw - min;
        if (w.Top > sh - min) w.Top = sh - min;
    }
}
