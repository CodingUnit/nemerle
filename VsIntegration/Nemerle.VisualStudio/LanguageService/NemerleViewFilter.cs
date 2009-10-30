using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

using Nemerle.Completion2;
using Nemerle.Compiler;
using Nemerle.Completion2.CodeFormatting;
using Nemerle.VisualStudio.Project;
using Nemerle.VisualStudio.GUI;
using TupleIntInt = Nemerle.Builtins.Tuple<int, int>;

using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;
using Microsoft.VisualStudio.Project.Automation;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Project;
using Nemerle.Compiler.Utils.Async;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Nemerle.VisualStudio;

namespace Nemerle.VisualStudio.LanguageService
{
	class NemerleViewFilter : ViewFilter
	{
		public NemerleViewFilter(CodeWindowManager mgr, IVsTextView view)
			: base(mgr, view)
		{
    }

		public override void HandleGoto(VsCommands cmd)
		{
			int line, col;

			// Get the caret position
			ErrorHandler.ThrowOnFailure(this.TextView.GetCaretPos(out line, out col));
			bool gotoDefinition = false;
			switch (cmd)
			{
				case VSConstants.VSStd97CmdID.GotoDecl:
				case VSConstants.VSStd97CmdID.GotoDefn: gotoDefinition = true; break;

				case VSConstants.VSStd97CmdID.GotoRef:
					Trace.WriteLine("GoToReference() not implemenred yet");
					break;
				default: Trace.Assert(false);	break;
			}

			Source.Goto(TextView, gotoDefinition, line, col);
		}
		
    #region GetDataTipText

		public override TextTipData CreateTextTipData()
		{
			return base.CreateTextTipData();
		}

    public override int GetDataTipText(TextSpan[] aspan, out string textValue)
    {
      //VladD2: ��������� ����������� �����:
      // � ��� ���� ��� �������� ������ ����������� ������ 1. �� ����� ����������. 2. �� ����� �������.
      // � ����� ������, ������ ����������� ������� ������� ����������� � ������� ������.
      // � ��� ����������� ������ ������� ����� ������������ � �������� ��������� �� ������� ��������� ������ (aspan[0]).
      // ����� ���� ����������� ��������� �� ��� ���������� �� �� � ���� ��� �����������, �� �������� ������,
      // ��� ��������� ����� ���������� - hint � ����������� � ���������, ��� DataHint ������� ���������� �������� ���������
      // ��� �������.

      textValue = null;
      if (Source == null || Source.LanguageService == null || !Source.LanguageService.Preferences.EnableQuickInfo)
        return NativeMethods.E_FAIL;

      return Source.GetDataTipText(TextView, aspan, out textValue);
    }

    /// <summary>This method checks to see if the IVsDebugger is running, and if so, 
    /// calls it to get additional information about the current token and returns a combined result.
    /// You can return an HRESULT here like TipSuccesses2.TIP_S_NODEFAULTTIP.</summary>
    public override int GetFullDataTipText(string textValue, TextSpan ts, out string fullTipText)
    {
      IVsTextLines textLines;
      fullTipText = textValue;

      ErrorHandler.ThrowOnFailure(this.TextView.GetBuffer(out textLines));

      // Now, check if the debugger is running and has anything to offer
      try
      {
        Microsoft.VisualStudio.Shell.Interop.IVsDebugger debugger = Source.LanguageService.GetIVsDebugger();

        if (debugger != null && Source.LanguageService.IsDebugging)
        {
          var tsdeb = new TextSpan[1] { new TextSpan() };
          if (!TextSpanHelper.IsEmpty(ts))
          {
            // While debugging we always want to evaluate the expression user is hovering over
            ErrorHandler.ThrowOnFailure(TextView.GetWordExtent(ts.iStartLine, ts.iStartIndex, (uint)WORDEXTFLAGS.WORDEXT_FINDEXPRESSION, tsdeb));
            // If it failed to find something, then it means their is no expression so return S_FALSE
            if (TextSpanHelper.IsEmpty(tsdeb[0]))
            {
              return NativeMethods.S_FALSE;
            }
          }
          string debugTextTip = null;
          int hr = debugger.GetDataTipValue(textLines, tsdeb, null, out debugTextTip);
          fullTipText = debugTextTip;
          if (hr == (int)TipSuccesses2.TIP_S_NODEFAULTTIP)
          {
            return hr;
          }
          if (!string.IsNullOrEmpty(debugTextTip) && debugTextTip != textValue)
          {
            // The debugger in this case returns "=value [type]" which we can
            // append to the variable name so we get "x=value[type]" as the full tip.
            int i = debugTextTip.IndexOf('=');
            if (i >= 0)
            {
              string spacer = (i < debugTextTip.Length - 1 && debugTextTip[i + 1] == ' ') ? " " : "";
              fullTipText = textValue + spacer + debugTextTip.Substring(i);
            }
          }
        }
#if LANGTRACE
            } catch (COMException e) {
                Trace.WriteLine("COMException: GetDataTipValue, errorcode=" + e.ErrorCode);
#else
      }
      catch (System.Runtime.InteropServices.COMException)
      {
#endif
      }
      
      if (string.IsNullOrEmpty(fullTipText))
        fullTipText = textValue;

      return NativeMethods.S_OK;
    }
 
    #endregion
		public override void OnKillFocus(IVsTextView view)
		{
			var pkg = Source.Service.Package;
			pkg.RefactoringMenu.Visible = false;
			pkg.SelectionExtend.Visible = false;
			pkg.SelectionShrink.Visible = false;
			pkg.SelectionExtend.Enabled = false;
			pkg.SelectionShrink.Enabled = false;

			//Debug.WriteLine("OnKillFocus(IVsTextView view)");
			base.OnKillFocus(view);
		}

		public override void OnSetFocus(IVsTextView view)
		{
			var pkg = Source.Service.Package;
			pkg.RefactoringMenu.Visible = true;
			pkg.SelectionExtend.Visible = true;
			pkg.SelectionShrink.Visible = true;
			pkg.SelectionExtend.Enabled = true;
			pkg.SelectionShrink.Enabled = true;

			//Debug.WriteLine("OnSetFocus(IVsTextView view)");
			//ShowAst(view, true);
			base.OnSetFocus(view);
    
      var source = Source;

      if (source != null)
        source.OnSetFocus(view); // notify source
    }

		private void ShowAst(IVsTextView view, bool showInfo)
		{
			NemerleSource source = Source as NemerleSource;
			if (source != null && source.ProjectInfo != null)
			{
				int line, col;
				ErrorHandler.ThrowOnFailure(view.GetCaretPos(out line, out col));
				//Debug.WriteLine(
				//		string.Format("OnChangeScrollInfo line={0}, col={1}", line + 1, col + 1));
				AstToolWindow tw = (AstToolWindow)source.ProjectInfo.ProjectNode.Package
					.FindToolWindow(typeof(AstToolWindow), 0, true);

				if (showInfo)
					tw.ShowInfo(source);

				tw.Activate(line + 1, col + 1);
			}
		}

		Dictionary<IntPtr, TupleIntInt> _viewsCarretInfo = new Dictionary<IntPtr, TupleIntInt>();

		public override void OnChangeScrollInfo(IVsTextView view, int iBar,
			int iMinUnit, int iMaxUnits, int iVisibleUnits, int iFirstVisibleUnit)
		{
			base.OnChangeScrollInfo(view, iBar, iMinUnit, iMaxUnits, iVisibleUnits, iFirstVisibleUnit);

			var viewUnknown = Utilities.QueryInterfaceIUnknown(view);
			if (viewUnknown != IntPtr.Zero)
				Marshal.Release(viewUnknown);

			TupleIntInt pos;
			var posExists = _viewsCarretInfo.TryGetValue(viewUnknown, out pos);
			int line, idx;
			if (view.GetCaretPos(out line, out idx) == VSConstants.S_OK)
			{
				if (!posExists || pos.Field0 != line || pos.Field1 != idx)
				{
					// pos was changed
					_viewsCarretInfo[viewUnknown] = new TupleIntInt(line, idx);
					Source.CaretChanged(view, line, idx);
					//Source.TryHighlightBraces1(view);
					//Debug.WriteLine("pos was changed line=" + line + " col=" + idx);
				}
			}
		}

		public override void ShowContextMenu(int menuId, Guid groupGuid, IOleCommandTarget target, int x, int y)
		{
			var service = Source.Service;
			var menuService = (OleMenuCommandService)service.GetService(typeof(IMenuCommandService));
			if (menuService == null || service.IsMacroRecordingOn())
				return;

			var id = new CommandID(groupGuid, menuId);
			menuService.ShowContextMenu(id, x, y);

			return;
		}

		public override void OnChangeCaretLine(IVsTextView view, int line, int col)
		{
			base.OnChangeCaretLine(view, line, col);
		}

		protected override int ExecCommand(ref Guid guidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			//Debug.WriteLine(guidCmdGroup + " " + nCmdId);
			const uint ShowSmartTag = 147;
			if (guidCmdGroup == VSConstants.VSStd2K && nCmdId == ShowSmartTag)
			{
				Source.ShowTypeNameSmartTag(TextView, true);
				return VSConstants.S_OK;
			}

			// hi_octane : found a lot of mistakes comparing the switch
			// ids with PkgCmdID.h and NemerleMenus.cs
			// decided modify the code
			// leaving only two files required to be synchronized

			string txt = null;
			switch((MenuCmd.CmdId)nCmdId)
			{
				case MenuCmd.CmdId.IplementInterface:

					break;
				case MenuCmd.CmdId.SetAsMain: 
					txt = "cmdidSetAsMain"; 
					break;
				case MenuCmd.CmdId.ExtendSelection:
					// cmdIdExtendSelection
					ExpandSelection();
					// it's prevent repeated execution of comand in base.ExecCommand() 
					return VSConstants.S_OK;
				case MenuCmd.CmdId.ShrinkSelection:
					// cmdIdShrinkSelection
					ShrinkSelection();
					return VSConstants.S_OK;
				case MenuCmd.CmdId.FindInheritors: //cmdIdFindInheritors 
				case MenuCmd.CmdId.FindInheritorsCtxt: //cmdIdFindInheritorsCtxt
					FindInheritors();
					return VSConstants.S_OK;
				case MenuCmd.CmdId.Rename: 
					txt = "cmdIdRename";
					RunRenameRefactoring();
					return VSConstants.S_OK;
				case MenuCmd.CmdId.Inline: // cmdIdInline
					RunInlineRefactoring();
					return VSConstants.S_OK;
				case MenuCmd.CmdId.Options:
					// cmdIdOptions
					ShowOptions();
					return VSConstants.S_OK;
				case MenuCmd.CmdId.AstToolWindow: // AstToolWindow
          Source.ProjectInfo.ProjectNode.Package.OnAstToolWindowShow(null, null);
					return VSConstants.S_OK;
				case MenuCmd.CmdId.AddHighlighting: // cmdIdAddHighlighting
					HighlightSymbol();
					return VSConstants.S_OK;
				case MenuCmd.CmdId.ESC: // ESC
				case MenuCmd.CmdId.RemoveLastHighlighting: // cmdIdRemoveLastHighlighting
					RemoveLastHighlighting();
          Source.Service.Hint.Close();
					if(nCmdId == (int)MenuCmd.CmdId.ESC) // ESC
						break; // go trocess ESC
					return VSConstants.S_OK;
				case MenuCmd.CmdId.SourceOutlinerWindow:
					{
						if(Source != null)
							Source.ProjectInfo.ProjectNode.Package.OnSourceOutlinerWindowShow(null, null);
					}
					return VSConstants.S_OK;
			}

			Trace.Assert(txt == null, "Implement the menu!\r\nID: " + txt);

      _executingCommand = (VsCommands2K)nCmdId;
      try
      {
        var result = base.ExecCommand(ref guidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut);
        return result;
      }
      finally { _executingCommand = 0; }
		}

    VsCommands2K _executingCommand;

		private List<Location> _selectionsStack;

		private int _currentSelection;
		public  int  CurrentSelection
		{
			get { return _currentSelection; }
			set
			{
				if (_selectionsStack != null && value != _currentSelection && value >= 0 && value < _selectionsStack.Count)
				{
					_currentSelection = value;
					TextSpan span = Utils.SpanFromLocation(_selectionsStack[_currentSelection]);
					TextView.SetSelection(span.iEndLine, span.iEndIndex, span.iStartLine, span.iStartIndex);
				}
			}
		}

		public new NemerleSource Source
		{
			get { return (NemerleSource)base.Source; }
		}

		public IWin32Window TextEditorWindow
		{
			get { return NativeWindow.FromHandle(TextView.GetWindowHandle()); }
		}

		private void ExpandSelection()
		{
			TextSpan selection = GetSelection();
			if (!SelectionIsInStack(selection))
			{
				_selectionsStack = GetSelectionsStack(selection);
				AddWordSpan(selection);
				_currentSelection = _selectionsStack.Count;
			}
			CurrentSelection--;
		}

		private void ShrinkSelection()
		{
			if (SelectionIsInStack(GetSelection()))
				CurrentSelection++;
		}

		private void HighlightSymbol()
		{
			NemerleSource source = Source;

			if (source != null)
			{
				TextSpan span = GetSelection();
        if (source.ProjectInfo == null)
          return;

        source.GetEngine().BeginHighlightUsages(source, span.iStartLine + 1, span.iStartIndex + 1);
			}
		}

		private void RemoveLastHighlighting()
		{
			if (Source != null && Source.ProjectInfo != null)
				Source.ProjectInfo.RemoveLastHighlighting(Source);
		}

		private bool WarnAboutErrors(Nemerle.Completion2.Project project)
		{
			foreach (var cm in project.Errors)
				if (cm.Kind == MessageKind.Error)
					return MessageBox.Show(TextEditorWindow, "The project contaions error[s]. Are you sure you want to proceed (it's unsafe!)?",
										"",
										MessageBoxButtons.YesNo,
										MessageBoxIcon.Exclamation) == DialogResult.Yes;
			return true;
		}

		private void RunInlineRefactoring()
		{
			if (Source == null) return;
			var proj = Source.ProjectInfo.Project;
			var engine = Source.ProjectInfo.Engine;

			if(!WarnAboutErrors(proj)) return;

			int lineIndex;
			int colIndex;
			TextView.GetCaretPos(out lineIndex, out colIndex);
      GotoInfo[] definitions = engine.GetGotoInfo(Source,
																	 lineIndex + 1,
																	 colIndex,
                                   Nemerle.Compiler.Utils.Async.GotoKind.Definition);
			if (definitions == null || definitions.Length == 0)
				return;

			if (definitions.Length > 1)
			{
				MessageBox.Show(TextEditorWindow, "More than one definition found. Cannot inline.");
				return;
			}

			if (proj.CanInlineExpressionAt(definitions[0]))
			{
				HighlightSymbol();

				using (var undoTransaction = new LinkedUndoTransaction("Inline refactoring", Source.Service.Site))
				{
					// replacing usages with initializer
					var tuple = proj.GetReplacementStringForInline(definitions[0]);
					var defLocation = tuple.Field0;
					var initLocation = tuple.Field1;
					var replacement = Source.GetText(Utils.SpanFromLocation(initLocation));
					var shouldEmbrace = tuple.Field2;
          var usages = engine.GetGotoInfo(Source,
														 lineIndex + 1,
														 colIndex,
                             GotoKind.Usages)
											.Where(usage => usage.UsageType == UsageType.Usage)
											.ToArray();
					using (var frm = new InlineRefactoringPreview(proj))
					{
						frm.Usages = usages;
						frm.ExpressionToInline = shouldEmbrace
													? string.Format("({0})", replacement)
													: replacement;
						if (frm.ShowDialog(TextEditorWindow) != DialogResult.OK)
						{
							RemoveLastHighlighting();
							return;
						}
					}
					Source.RenameSymbols(replacement, usages, false);

					// replacing definition with empty string
					Source.RenameSymbols("", new[] { new GotoInfo(defLocation) });

					Source.DeleteEmptyStatementAt(defLocation.Line - 1);

					undoTransaction.Commit();
				}
				RemoveLastHighlighting();
			}
			else
			{
				MessageBox.Show(TextEditorWindow, "Only simple def's can be inlined at the moment.");
			}
		}

		private void RunRenameRefactoring()
		{
			if (Source == null || Source.ProjectInfo == null)
        return;
      var proj   = Source.ProjectInfo.Project;
      var engine = Source.ProjectInfo.Engine;
			if(!WarnAboutErrors(proj))
        return;

			int lineIndex;
			int colIndex;
			TextView.GetCaretPos(out lineIndex, out colIndex);
      var allUsages = engine.GetGotoInfo(Source, lineIndex + 1, colIndex + 1, GotoKind.Usages);
			var usages = allUsages.Distinct().ToArray();
      if (usages == null || usages.Length == 0)
      {
        Source.ProjectInfo.ShowMessage("No symbol to rename", MessageType.Error);
        return;
      }

			var definitionCount = usages.Count(usage => usage.UsageType == UsageType.Definition);
			if(definitionCount == 0)
			{
        Source.ProjectInfo.ShowMessage("Cannot find definition.", MessageType.Error);
				return;
			}
			if(definitionCount > 1)
			{
        Source.ProjectInfo.ShowMessage("More than one definition found. Must be error in Find Usages.", MessageType.Error);
        return;
			}

			using (var frm = new RenameRefactoringDlg(proj, usages))
			{
				if (frm.ShowDialog(TextEditorWindow) == DialogResult.OK)
				{
					Source.RenameSymbols(frm.NewName, usages);
				}
			}
			RemoveLastHighlighting();
		}

		private void FindInheritors()
		{
			if (Source == null)
				return;

			NemerleLanguageService ourLanguageService = Source.LanguageService as NemerleLanguageService;
			if (ourLanguageService == null)
				return;

			int lineIndex;
			int colIndex;
			TextView.GetCaretPos(out lineIndex, out colIndex);
			GotoInfo[] infos =
				Source.ProjectInfo.Project.GetInheritors(Source.FileIndex, lineIndex + 1, colIndex + 1);

			// If we have only one found usage, then jump directly to it.
			if (infos.Length == 1)
				ourLanguageService.GotoLocation(infos[0].Location);
			else if (infos.Length > 0) // otherwise show a form to let user select entry manually
			{
				using (GotoUsageForm popup = new GotoUsageForm(infos))
					if (popup.ShowDialog(TextEditorWindow) == DialogResult.OK)
						ourLanguageService.GotoLocation(popup.Result.Location);
			}
		}

		private bool SelectionIsInStack(TextSpan selection)
		{
			// perhaps, using OnSelectChange routine would be better
			// to determine, whether user already moved selection or still in the selection chain
			if (_selectionsStack == null || _selectionsStack.Count == 0)
				return false;
			Location current = Utils.LocationFromSpan(_selectionsStack[0].FileIndex, selection);

			int i = _selectionsStack.IndexOf(current);
			if (i >= 0)
			{
				_currentSelection = i;
				return true;
			}

			return false;
		}

		private void ShowOptions()
		{
			using (Options options = new Options())
			{
				if (options.ShowDialog(TextEditorWindow) == DialogResult.OK)
					options.Save();
			}
		}

		private void AddWordSpan(TextSpan selection)
		{
			TextSpan[] spans = new TextSpan[1];
			// HACK: have seen 4 as usual flags value for GetWordExtent ;)
			uint flags = 4;
			GetWordExtent(selection.iStartLine, selection.iStartIndex, flags, spans);
			TextSpan wordSpan = spans[0];
			GetWordExtent(selection.iEndLine, selection.iEndIndex, flags, spans);
			TextSpan wordSpanOther = spans[0];
			// in principle, selectionsStack contains at least the whole file location
			if (wordSpan.Equals(wordSpanOther) && _selectionsStack != null && _selectionsStack.Count > 0)
			{
				Location last = _selectionsStack[_selectionsStack.Count - 1];
				Location word = Utils.LocationFromSpan(last.FileIndex, wordSpan);
				if (last.StrictlyContains(word) && last != word)
					_selectionsStack.Add(word);
			}
			else
			{
				_selectionsStack = new List<Location>();
				_selectionsStack.Add(Utils.LocationFromSpan(0, wordSpan));
			}
		}

		private List<Location> GetSelectionsStack(TextSpan span)
		{
			NemerleSource source = Source as NemerleSource;
			if (source != null)
			{
				Location current = Utils.LocationFromSpan(source.FileIndex, span);
				List<Location> stack = new List<Location>();
				stack.AddRange(source.ProjectInfo.Project.GetEnclosingLocationsChain(current).ToArray());
				return stack;
			}
			else
				return null;
		}

		//public override int GetWordExtent(int line, int index, uint flags, TextSpan[] span)
		//{
    //	System.Diagnostics.Debug.Assert(false, "line " + line + " index " + index + " flags " + flags + " span length " + span.Length);

		//	base.GetWordExtent(line, index, flags, span);
		//	return 0;
		//}

		protected override int QueryCommandStatus(ref Guid guidCmdGroup, uint nCmdId)
		{
			if (guidCmdGroup == VSConstants.VSStd2K)
			{
				var cmdId = (VSConstants.VSStd2KCmdID)nCmdId;

				if (cmdId == VSConstants.VSStd2KCmdID.INSERTSNIPPET
				 || cmdId == VSConstants.VSStd2KCmdID.SURROUNDWITH)
				{
					return (int)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
				}
			}

			if (guidCmdGroup == Microsoft.VisualStudio.Shell.VsMenus.guidStandardCommandSet97)
			{
				//object objCaption;
				//GetProperty(itemid, (int)__VSHPROPID.VSHPROPID_Caption, out objCaption);

				//if (objCaption.ToString().Equals("MyItems", StringComparison.CurrentCultureIgnoreCase))
				//{
				//  // make Properties command invisible.
				//  prgCmds[0].cmdf = (uint)OLECMDF.OLECMDF_SUPPORTED | (uint)OLECMDF.OLECMDF_INVISIBLE;
				//  return VSConstants.S_OK;
				//}
				//return (int)OLECMDF.OLECMDF_SUPPORTED | (int)OLECMDF.OLECMDF_INVISIBLE; //OLECMDF_SUPPORTED OLECMDF_ENABLED
			}

			//return (int)OLECMDF.OLECMDF_SUPPORTED | (int)OLECMDF.OLECMDF_INVISIBLE; //OLECMDF_SUPPORTED OLECMDF_ENABLED

			if (guidCmdGroup == MenuCmd.guidNemerleProjectCmdSet)
			{
				MenuCmd.CmdId x = (MenuCmd.CmdId)nCmdId;
				switch (x)
				{
					case MenuCmd.CmdId.ESC:
					case MenuCmd.CmdId.SetAsMain:
					case MenuCmd.CmdId.ExtendSelection:
					case MenuCmd.CmdId.ShrinkSelection:
					case MenuCmd.CmdId.FindInheritors:
					case MenuCmd.CmdId.Rename:
					case MenuCmd.CmdId.Inline:
					case MenuCmd.CmdId.Options:
					case MenuCmd.CmdId.AstToolWindow:
					case MenuCmd.CmdId.AddHighlighting:
					case MenuCmd.CmdId.RemoveLastHighlighting:
					case MenuCmd.CmdId.FindInheritorsCtxt:
					case MenuCmd.CmdId.GoToFile:
					case MenuCmd.CmdId.GoToType:
					case MenuCmd.CmdId.SourceOutlinerWindow:
						break;
					case MenuCmd.CmdId.IplementInterface:

						return (int)OLECMDF.OLECMDF_SUPPORTED | (int)OLECMDF.OLECMDF_ENABLED; //OLECMDF_SUPPORTED OLECMDF_ENABLED

					default:

						break;
				}
			}

			return base.QueryCommandStatus(ref guidCmdGroup, nCmdId);
		}

		int _startLine;
		int _startPos;

		public override void CommentSelection()
		{
			TextSpan ts = GetRealSelectionSpan();
			Source.CommentLines(ts, "//");
		}
		public override void UncommentSelection()
		{
			TextSpan ts = GetRealSelectionSpan();
			Source.UncommentLines(ts, "//");
		}

		private TextSpan GetRealSelectionSpan()
		{
			// workaround for bug #1050
			// TextView.GetSelection adds caret position to the selection span,
			// so when we select whole block of text (caret is at the first column),
			// one extra line is added to the selection (where the caret is left)
			TextSpan ts = new TextSpan();
			TextView.GetSelection(out ts.iStartLine, out ts.iStartIndex, out ts.iEndLine, out ts.iEndIndex);
			if (ts.iEndIndex == 0 && ts.iEndLine - ts.iStartLine > 0)
				ts.iEndLine--;
			return ts;
		}

		public override bool HandlePreExec(
			ref Guid guidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			var cmd = (VsCommands2K)nCmdId;

      //// we're goona to erase some symbol from existence. 
      //// In some cases we need to know what it was (auto-deletion of paired token)
      //if (cmd == VsCommands2K.BACKSPACE)
      //  Source.RememberCharBeforeCaret(TextView);
      //else
      //  Source.ClearRememberedChar();

			_startLine = -1;

			if (guidCmdGroup == VSConstants.VSStd2K)
			{
				if (Source.MethodData.IsDisplayed)
					TextView.GetCaretPos(out _startLine, out _startPos);



				switch (cmd)
				{
					case VsCommands2K.COMPLETEWORD:
						{
							int lintIndex;
							int columnInxex;
							ErrorHandler.ThrowOnFailure(TextView.GetCaretPos(out lintIndex, out columnInxex));
              Source.Completion(TextView, lintIndex, columnInxex, false);
							return true;
						}
					case VsCommands2K.FORMATSELECTION:
						ReformatSelection();
						return true;

					case VsCommands2K.INSERTSNIPPET:
						{
							ExpansionProvider ep = GetExpansionProvider();

							if (TextView != null && ep != null)
								ep.DisplayExpansionBrowser(TextView, Resources.InsertSnippet, null, false, null, false);

							return true; // Handled the command.
						}
					case VsCommands2K.SURROUNDWITH:
						{
							ExpansionProvider ep = GetExpansionProvider();

							if (TextView != null && ep != null)
								ep.DisplayExpansionBrowser(TextView, Resources.SurroundWith, null, false, null, false);

							return true; // Handled the command.
						}

					case VsCommands2K.UP:
						if (Source.MethodData.IsDisplayed && Source.MethodData.GetCurMethod() == 0)
						{
							int count = Source.MethodData.GetOverloadCount();

							if (count > 1)
							{
								while (Source.MethodData.NextMethod() < count - 1)
									Source.MethodData.UpdateView();

								return true;
							}
						}

						break;

					case VsCommands2K.DOWN:
						if (Source.MethodData.IsDisplayed)
						{
							int count = Source.MethodData.GetOverloadCount();

							if (count > 1 && Source.MethodData.GetCurMethod() == count - 1)
							{
								while (Source.MethodData.PrevMethod() > 0)
									Source.MethodData.UpdateView();

								return true;
							}
						}

						break;
				}
			}

			// Base class handled the command.  Do nothing more here.
			//
			return base.HandlePreExec(ref guidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut);
		}

		// TODO: Implement smart indention
		// 1. When typing open curly brace (second is entered automatically) and 
		//		then pressing enter
		// 2. When typing enter between sentences.
		// 3. When typing enter in the middle of expression.
		public override bool HandleSmartIndent()
		{
			// TODO: Minimal functionality is to find what caret position should be,
			// and to insert needed whitespace to move all the text that is after caret
			// to the position after preferred caret position.

            //int line;
            //int idx;
            //TextView.GetCaretPos(out line, out idx);

            //string filePath = Source.GetFilePath();
            //ProjectInfo projectInfo = ProjectInfo.FindProject(filePath);
            //Engine engine = projectInfo.Engine;

            //List<FormatterResult> results = Formatter.FormatExpressionAt(engine, filePath, line + 1, idx + 1);
            //ApplyFormatterResults(results);

			//MessageBox.Show(TextEditorWindow, "Caret pos in HandleSmartIndent: " + line + ":" + col);
			return false;
		}

		public override void HandlePostExec(
			ref Guid guidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut, bool bufferWasChanged)
		{
			VsCommands2K cmd = (VsCommands2K)nCmdId;
			// Special handling of "Toggle all outlining" command
			if (guidCmdGroup == typeof(VsCommands2K).GUID)
			{
				if ((VsCommands2K)nCmdId == VsCommands2K.OUTLN_TOGGLE_ALL)
				{
					Source.CollapseAllRegions();
					return;
				}
			}

			base.HandlePostExec(ref guidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut, bufferWasChanged);

			// workaround: for some reason, UP and DOWN commands are not passed to Source in base.HandlePostExec
			if (cmd == VsCommands2K.UP || cmd == VsCommands2K.DOWN)
				Source.OnCommand(TextView, cmd, '\0');

			if (_startLine >= 0 && Source.MethodData.IsDisplayed)
			{
				int line;
				int pos;

				TextView.GetCaretPos(out line, out pos);

				if (line != _startLine || pos != _startPos)
				{
					bool backward =
						cmd == VsCommands2K.BACKSPACE ||
						cmd == VsCommands2K.BACKTAB ||
						cmd == VsCommands2K.LEFT ||
						cmd == VsCommands2K.LEFT_EXT;

					TokenInfo info = Source.GetTokenInfo(line, pos);
					TokenTriggers triggerClass = info.Trigger;

					if (!backward && (triggerClass & TokenTriggers.MethodTip) == TokenTriggers.ParameterNext)
					{
						Source.MethodData.AdjustCurrentParameter(1);
					}
					else
					{
						Source.MethodTip(TextView, line, pos, info);
					}
				}
			}
		}

		private void ApplyFormatterResults(List<FormatterResult> results)
		{
			foreach (FormatterResult result in results)
			{
				if (result.StartLine == result.EndLine)
				{
					int line = result.StartLine - 1;
					Debug.Assert(Source.GetLineLength(line) >= result.EndCol - 1, "Line must be GE that formatter result");
					Source.SetText(line, result.StartCol - 1, line, result.EndCol - 1,
									 result.ReplacementString);
				}
				else
					Source.SetText(result.StartLine, result.StartCol - 1, result.EndLine - 1, result.EndCol - 1,
								 result.ReplacementString);
			}
		}

		/// <summary>
		/// Here we will do our formatting...
		/// </summary>
		//public override void ReformatSelection()
		//{
		//	if (this.CanReformat())
		//	{

		//		Debug.Assert(this.Source != null);
		//		if (this.Source != null)
		//		{
		//			TextSpan ts = GetSelection();
		//			if (TextSpanHelper.IsEmpty(ts))
		//			{
		//				// format just this current line.
		//				ts.iStartIndex = 0;
		//				ts.iEndLine = ts.iStartLine;
		//				ts.iEndIndex = this.Source.GetLineLength(ts.iStartLine);
		//			}
		//			Formatter.FormatSpan(ts.iStartLine, ts.iEndLine);
		//			//using (EditArray mgr = new EditArray(this.Source, this.TextView, true, "Formatting"))
		//			//{
		//			//	this.Source.ReformatSpan(mgr, ts);
		//			//	mgr.ApplyEdits();
		//			//}
		//		}
		//	}

		//	//base.ReformatSelection();
		//}
	}
}
