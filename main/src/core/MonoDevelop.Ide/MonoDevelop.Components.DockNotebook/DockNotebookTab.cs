//
// DockNotebookTab.cs
//
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (http://xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;

using Gtk;
using Xwt.Motion;

using MonoDevelop.Components;
using MonoDevelop.Components.AtkCocoaHelper;

namespace MonoDevelop.Components.DockNotebook
{
	class DockNotebookTab: IAnimatable
	{
		DockNotebook notebook;
		readonly TabStrip strip;

		internal AtkCocoaHelper.AccessibilityElementProxy Accessible { get; private set; }
		internal AtkCocoaHelper.AccessibilityElementProxy CloseButtonAccessible { get; private set; }

		string text;
		string markup;
		string tooltip;
		Xwt.Drawing.Image icon;
		Widget content;

		Gdk.Rectangle allocation;
		internal Gdk.Rectangle Allocation {
			get {
				return allocation;
			}
			set {
				Gdk.Rectangle cocoaFrame;

				cocoaFrame.X = value.X;

				// This will fail if Y != 0
				cocoaFrame.Y = value.Y;
				cocoaFrame.Width = value.Width;
				cocoaFrame.Height = value.Height;

				Accessible.SetFrameInParent (cocoaFrame);
				Accessible.SetFrameInGtkParent (value);
				allocation = value;
			}
		}

		Cairo.Rectangle closeButtonActiveArea;
		internal Cairo.Rectangle CloseButtonActiveArea {
			get {
				return closeButtonActiveArea;
			}
			set {
				Gdk.Rectangle cocoaFrame;

				// value is in the TabStrip's coordinate space, whereas we need to set the button in the tab space.
				cocoaFrame.X = (int)value.X - allocation.X;
				int halfParentWidth = allocation.Height / 2;
				double dy = value.Y - halfParentWidth;
				cocoaFrame.Y = (int) (halfParentWidth + dy) - allocation.Y;
				cocoaFrame.Width = (int) value.Width;
				cocoaFrame.Height = (int) value.Height;

				CloseButtonAccessible.SetFrameInParent (cocoaFrame);

				Gdk.Rectangle realFrame = new Gdk.Rectangle ((int) value.X, (int) value.Y, (int) value.Width, (int) value.Height);
				CloseButtonAccessible.SetFrameInGtkParent (realFrame);
				closeButtonActiveArea = value;
			}
		}

		public DockNotebook Notebook { get { return notebook; } }

		public int Index { get; internal set; }

		public bool Notify { get; set; }

		public double WidthModifier { get; set; }

		public double Opacity { get; set; }

		public double GlowStrength { get; set; }

		public bool Hidden { get; set; }

		public double DirtyStrength { get; set; }
		
		void IAnimatable.BatchBegin () { }
		void IAnimatable.BatchCommit () { QueueDraw (); }

		bool dirty;
		public bool Dirty {
			get { return dirty; }
			set { 
				if (dirty == value)
					return;
				dirty = value;
				this.Animate ("Dirty", f => DirtyStrength = f,
				              easing: Easing.CubicInOut,
				              start: DirtyStrength, end: value ? 1 : 0);

				string accTitle;

				if (dirty) {
					accTitle = string.Format (Core.GettextCatalog.GetString ("{0}. (dirty)"), Text ?? Markup);
				} else {
					accTitle = Text ?? Markup;
				}
				Accessible.SetAccessibilityTitle (accTitle);
			}
		}

		public string Text {
			get {
				return text;
			}
			set {
				text = value;
				markup = null;

				string accTitle;

				if (dirty) {
					accTitle = string.Format (Core.GettextCatalog.GetString ("{0}. (dirty)"), value);
				} else {
					accTitle = value;
				}

				Accessible.SetAccessibilityTitle (accTitle);
				CloseButtonAccessible.SetAccessibilityTitle (string.Format (Core.GettextCatalog.GetString ("Close {0}"), value));

				strip.Update ();
			}
		}

		public string Markup {
			get {
				return markup;
			}
			set {
				markup = value;
				text = null;

				// FIXME: Strip markup
				string accTitle;
				if (dirty) {
					accTitle = string.Format (Core.GettextCatalog.GetString ("{0}. (dirty)"), value);
				} else {
					accTitle = value;
				}

				Accessible.SetAccessibilityTitle (accTitle);
				CloseButtonAccessible.SetAccessibilityTitle (string.Format (Core.GettextCatalog.GetString ("Close {0}"), value));

				strip.Update ();
			}
		}

		public Xwt.Drawing.Image Icon {
			get {
				return icon;
			}
			set {
				icon = value;
				strip.Update ();
			}
		}

		public Widget Content {
			get {
				return content;
			}
			set {
				content = value;
				notebook.ShowContent (this);
			}
		}

		public string Tooltip {
			get {
				return tooltip;
			}
			set {
				tooltip = value;
				Accessible.SetAccessibilityHelp (string.Format (Core.GettextCatalog.GetString ("Switch to {0}"), value));
			}
		}

		internal DockNotebookTab (DockNotebook notebook, TabStrip strip)
		{
			Accessible = new AtkCocoaHelper.AccessibilityElementButtonProxy ();
			Accessible.PerformPress += OnPressTab;
			// FIXME Should Role descriptions be translated?
			Accessible.SetAccessibilityRole (AtkCocoa.Roles.AXRadioButton, "tab");
			Accessible.SetGtkParent (strip);
			Accessible.Actions = new string [] { "AXShowMenu" };
			Accessible.PerformShowPopupMenu += OnShowMenu;
			Accessible.SetAccessibilityIdentifier ("DockNotebook.Tab");

			CloseButtonAccessible = new AtkCocoaHelper.AccessibilityElementButtonProxy ();
			CloseButtonAccessible.PerformPress += OnPressCloseButton;
			CloseButtonAccessible.SetAccessibilityRole (AtkCocoa.Roles.AXButton);
			CloseButtonAccessible.SetGtkParent (strip);
			CloseButtonAccessible.Actions = new string [] { "AXShowMenu" };
			CloseButtonAccessible.PerformShowPopupMenu += OnShowMenu;
			CloseButtonAccessible.SetAccessibilityTitle (Core.GettextCatalog.GetString ("Close document"));
			CloseButtonAccessible.SetAccessibilityIdentifier ("DockNotebook.Tab.CloseButton");
			Accessible.AddAccessibleChild (CloseButtonAccessible);

			this.notebook = notebook;
			this.strip = strip;
		}

		internal Gdk.Rectangle SavedAllocation { get; private set; }
		internal double SaveStrength { get; set; }

		internal void SaveAllocation ()
		{
			SavedAllocation = Allocation;
		}

		public void QueueDraw ()
		{
			strip.QueueDraw ();
		}

		internal event EventHandler AccessibilityPressTab;
		internal event EventHandler AccessibilityPressCloseButton;
		internal event EventHandler AccessibilityShowMenu;

		void OnPressTab (object sender, EventArgs args)
		{
			AccessibilityPressTab?.Invoke (this, args);
		}

		void OnShowMenu (object sender, EventArgs args)
		{
			AccessibilityShowMenu?.Invoke (this, args);
		}

		void OnPressCloseButton (object sender, EventArgs args)
		{
			AccessibilityPressCloseButton?.Invoke (this, args);
		}

		void OnCloseButtonShowMenu (object sender, EventArgs args)
		{
			AccessibilityShowMenu?.Invoke (this, args);
		}
	}
}
