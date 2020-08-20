/*
Attach this script to a RectTransform, probably the content of a scroll area,
to make it a MiniScript code editor.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Text.RegularExpressions;
using UnityEditor;
using System.Text;

namespace Miniscript
{
    public class CodeEditor : Selectable, IDragHandler
    {
        #region Public Properties

        public struct TextPosition
        {
            public int line;            // 0-based index into lines
            public int offset;          // how many characters on that line are to the left of this position

            public TextPosition(int line, int offset)
            {
                this.line = line;
                this.offset = offset;
            }

            public override string ToString()
            {
                return string.Format("{0}:{1}", line, offset);
            }
        }

        [Tooltip("Text or TextMeshProUGUI serving as the prototype for each source code line")]
        public TextMeshProUGUI sourceCodeLinePrototype;

        [Tooltip("Optional Text or TextMeshProUGUI prototype for a line number")]
        public TextMeshProUGUI lineNumberPrototype;

        [Tooltip("What number line numbers should start at")]
        public int lineNumbersStartAt = 1;

        [Tooltip("What format string should be used for line numbers")]
        public string lineNumberFormatString = "00000";

        [Tooltip("Insertion-point (probably blinking) cursor")]
        public Graphic caret;

        [Tooltip("Image used (and cloned as needed) to show the selection highlight")]
        public Graphic selectionHighlight;

        [Tooltip("Style to apply; if left null, will look on the parent for a CodeStyling component")]
        public CodeStyling style;

        [Tooltip("How long to wait before starting to generate repeat inputs when a key is held")]
        // Windows default is 250ms
        // https://superuser.com/questions/388160/keyboard-repeat-rate-repeat-delay-values-in-win7
        public float keyRepeatDelay = 0.250f;
        //public float keyRepeatDelay = 0.3f;

        [Tooltip("Interval between repeat inputs when a key is held")]
        // Windows default is 31ms
        // https://www.tenforums.com/tutorials/136866-change-keyboard-character-repeat-delay-rate-windows.html
        public float keyRepeatInterval = 0.031f;
        //public float keyRepeatInterval = 0.075f;

        [Tooltip("Max seconds between mouse-up and mouse-down to be considered a double-click")]
        public float doubleClickTime = 0.3f;

        [Tooltip("How many levels of undo/redo to support")]
        public int undoLimit = 20;

        public TextAsset initialSourceCode;

        private string _lastSource;
        private bool _isSourceDirty = false;
        public string source
        {
            get
            {
                if (!_isSourceDirty)
                    return _lastSource;

                _workingStringBuilder.Clear();
                for (int i = 0; i < sourceLines.Count; i++)
                {
                    sourceLines[i].AppendToStringBuilder(_workingStringBuilder);
                    if (i != sourceLines.Count - 1)
                        _workingStringBuilder.Append('\n');
                }
                _lastSource = _workingStringBuilder.ToString();
                _isSourceDirty = false;
                return _lastSource;
            }
            set
            {
                LoadSource(value);
            }
        }

        public List<SourceLine> SourceLines
        {
            get { return sourceLines; }
        }
        public bool canUndo
        {
            get { return undoPosition >= 0; }
        }
        public bool canRedo
        {
            get { return undoPosition + 2 < undoStack.Count; }
        }
        public bool isFocused
        {
            get { return hasFocus; }
        }

        #endregion

        public Action OnInputFieldSelected;
        public Action OnInputFieldDeselected;

        #region Private Properties

        [System.Serializable]
        public struct UndoState
        {
            public string source;
            public TextPosition selAnchor;
            public TextPosition selEndpoint;
        }

        const int ExpectedNumLines = 512;
        readonly List<SourceLine> sourceLines = new List<SourceLine>(ExpectedNumLines);                 // our current source, split into lines
        [SerializeField]
        List<TextMeshProUGUI> uiTexts = new List<TextMeshProUGUI>(ExpectedNumLines);          // Text or TMProUGUI that draw our source lines
        readonly List<TextMeshProUGUI> lineNums = new List<TextMeshProUGUI>(ExpectedNumLines);         // Text or TMProUGUI that draw our line numbers
        Graphic selHighlightMid;        // highlight used for the middle lines of a multi-line selection
        Graphic selHighlight2;          // selection highlight used at the end of a multi-line selection
        TextPosition selStartpoint;         // caret position, or start of an extended selection
        TextPosition selEndpoint;       // "active" end of an extended selection, if different from anchor
        float preferredX;               // caret X position within the line which we will set on up/down if possible
        Dictionary<KeyCode, float> keyRepeatTime;   // Time.realtimeSinceStartup at which a given key can repeat
        float caretOnTime;              // Time.realtimeSinceStartup at which caret started the "on" phase of its blink
        List<UndoState> undoStack;      // buffer of undo/redo states
        int undoPosition;               // index of next item to undo in UndoStack
        float lastEditTime;             // Time.realtimeSinceStartup of last edit (so we know when to combine with the last undo state)
        ScrollRect scrollRect;          // the scroll view we are the content of (if any)
        RectTransform scrollMask;       // viewport mask
        float mouseUpTime;              // Time.realtimeSinceStartup at which mouse last went up
        int clickCount;                 // 1 for single click, 2 for double-click, 3 for triple-click
        bool hasFocus;                  // true when we have the keyboard focus
        LayoutElement _layoutElement;
        readonly StringBuilder _workingStringBuilder = new StringBuilder(512);
        readonly Stack<TextMeshProUGUI> _availableSourcePrefabs = new Stack<TextMeshProUGUI>();
        RectTransform _rectTransform;
        RectTransform _parentRectTransform;
        /// <summary>
        /// The SourceLine used for the line numbers
        /// </summary>
        SourceLine _workingLineNumber = new SourceLine(8);
        /// <summary>
        /// The SourceLine used for markup
        /// </summary>
        SourceLine _workingMarkupSourceLine = new SourceLine(256);
        /// <summary>
        /// How much capacity should the SourceLines have.
        /// Should be a PoT, SourceLine will autoexpand as needed
        /// </summary>
        const int SourceLineDefaultCapacity = 128;

        bool extendedSelection { get { return selEndpoint.line != selStartpoint.line || selEndpoint.offset != selStartpoint.offset; } }

        TextPosition selEndFirst
        {
            get
            {
                if (selStartpoint.line < selEndpoint.line
                        || (selStartpoint.line == selEndpoint.line && selStartpoint.offset < selEndpoint.offset))
                {
                    return selStartpoint;
                }
                return selEndpoint;
            }
        }
        TextPosition selEndLast
        {
            get
            {
                if (selStartpoint.line > selEndpoint.line
                    || (selStartpoint.line == selEndpoint.line && selStartpoint.offset > selEndpoint.offset))
                {
                    return selStartpoint;
                }
                return selEndpoint;
            }
        }

        #endregion
        #region Unity Interface Methods

        protected override void Awake()
        {
            base.Awake();

            // Awake is called in editor mode as well,
            // so we exit early if we're not in play mode
            if (!Application.isPlaying)
                return;

            _rectTransform = transform as RectTransform;
            _parentRectTransform = transform as RectTransform;
            undoStack = new List<UndoState>();
            undoPosition = -1;
            sourceCodeLinePrototype.gameObject.SetActive(false);
            keyRepeatTime = new Dictionary<KeyCode, float>();
            if (style == null) style = GetComponentInParent<CodeStyling>();
            scrollRect = GetComponentInParent<ScrollRect>();
            Mask m = GetComponentInParent<Mask>();
            if (m != null) scrollMask = m.rectTransform;

            selHighlightMid = Instantiate(selectionHighlight, transform);
            selHighlight2 = Instantiate(selectionHighlight, transform);
            selHighlightMid.transform.SetAsFirstSibling();
            selHighlight2.transform.SetAsFirstSibling();

            lastEditTime = -1;
            selStartpoint = selEndpoint = new TextPosition(0, 0);
            _layoutElement = GetComponent<LayoutElement>();
        }

        protected override void Start()
        {
            if (!Application.isPlaying)
                return; // don't run this stuff in the editor!
            base.Start();

            if (initialSourceCode != null)
                LoadSource(initialSourceCode.text);
            else
                LoadSource("\n");

            UpdateSelectionDisplay();

            caret.enabled = false;
            Select();
        }
        private TextMeshProUGUI GetSourceCodePrefab()
        {
            if(_availableSourcePrefabs.Count > 0)
            {
                TextMeshProUGUI tmp = _availableSourcePrefabs.Pop();
                tmp.gameObject.SetActive(true);
                return tmp;
            }
            return Instantiate(sourceCodeLinePrototype, sourceCodeLinePrototype.transform.parent).GetComponent<TextMeshProUGUI>();
        }
        private void ReturnSourceCodePrefab(TextMeshProUGUI tmp)
        {
            tmp.gameObject.SetActive(false);
            _availableSourcePrefabs.Push(tmp);
        }
        private TextMeshProUGUI GetLineNumberPrefab()
        {
            return Instantiate(lineNumberPrototype, lineNumberPrototype.transform.parent).GetComponent<TextMeshProUGUI>();
        }
        public override void OnSelect(BaseEventData eventData)
        {
            //Debug.Log(gameObject.name + " now has the focus");
            base.OnSelect(eventData);
            hasFocus = true;
            if (OnInputFieldSelected != null)
                OnInputFieldSelected();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            //Debug.Log(gameObject.name + " lost focus " + eventData.ToString() + " selected " + eventData.selectedObject.name);
            //UnityEngine.EventSystems.AxisEventData
            base.OnDeselect(eventData);
            hasFocus = false;
            caret.enabled = false;
            if (OnInputFieldDeselected != null)
                OnInputFieldDeselected();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            float upTime = Time.realtimeSinceStartup - mouseUpTime;
            if (upTime > doubleClickTime) clickCount = 1;
            else clickCount++;

            Vector2 pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform,
                eventData.position, eventData.pressEventCamera, out pos);
            selEndpoint = PositionAtXY(pos);
            bool forward = (selEndpoint.line > selStartpoint.line
                || (selEndpoint.line == selStartpoint.line && selEndpoint.offset > selStartpoint.offset));
            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            {
                selStartpoint = selEndpoint;
            }
            ExtendSelection(forward);
            UpdateSelectionDisplay();
            preferredX = caret.rectTransform.anchoredPosition.x;

            eventData.Use();
        }
        public override void OnPointerUp(PointerEventData eventData)
        {
            mouseUpTime = Time.realtimeSinceStartup;

            eventData.Use();
        }
        public void OnDrag(PointerEventData eventData)
        {
            Vector2 pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform,
                eventData.position, eventData.pressEventCamera, out pos);
            selEndpoint = PositionAtXY(pos);
            bool forward = (selEndpoint.line > selStartpoint.line
                || (selEndpoint.line == selStartpoint.line && selEndpoint.offset > selStartpoint.offset));
            ExtendSelection(forward);
            UpdateSelectionDisplay();
            eventData.Use();
        }

        #region Edit
        [ContextMenu("Edit Cut")]
        public void EditCut()
        {
            GUIUtility.systemCopyBuffer = SelectedText();
            SetSelText("");
        }

        [ContextMenu("Edit Copy")]
        public void EditCopy()
        {
            GUIUtility.systemCopyBuffer = SelectedText();
        }

        [ContextMenu("Edit Paste")]
        public void EditPaste()
        {
            SetSelText(GUIUtility.systemCopyBuffer);
            ScrollIntoView(selEndpoint);
        }

        [ContextMenu("Edit Delete")]
        public void EditDelete()
        {
            SetSelText("");
            ScrollIntoView(selEndpoint);
        }

        [ContextMenu("Edit Select All")]
        public void EditSelectAll()
        {
            selStartpoint = new TextPosition(0, 0);
            selEndpoint = new TextPosition(sourceLines.Count - 1, sourceLines[sourceLines.Count - 1].Length);
            UpdateSelectionDisplay();
        }

        [ContextMenu("Edit Undo")]
        public void EditUndo()
        {
            if (undoPosition >= 0)
            {
                if (undoPosition == undoStack.Count - 1)
                {
                    // At the top of the stack; add another entry to redo to where we currently are.
                    undoStack.Add(GetUndoState());
                }
                ApplyUndo(undoStack[undoPosition]);
                undoPosition--;
            }
            ScrollIntoView(selEndpoint);
        }

        [ContextMenu("Edit Redo")]
        public void EditRedo()
        {
            if (undoPosition + 2 < undoStack.Count)
            {
                ApplyUndo(undoStack[undoPosition + 2]);
                undoPosition++;
            }
            ScrollIntoView(selEndpoint);
        }
#endregion

        public void Indent(int levels)
        {
            TextPosition startPos = selEndFirst;
            TextPosition endPos = selEndLast;
            for (int i = startPos.line; i <= endPos.line; i++)
            {
                SourceLine sourceLine = sourceLines[i];
                if (levels > 0)
                {
                    sourceLine.Prepend('\t', levels);
                    //s = "\t\t\t\t\t\t\t\t\t\t".Substring(0, levels) + s;
                }
                else
                {
                    for (int j = 0; j < -levels; j++)
                    {
                        char c = sourceLine.AtIndex(0);
                        if (c == '\t' || c == ' ')
                            sourceLine.TrimStart(1);
                    }
                }
                UpdateLine(i, ref sourceLine);
            }
            selStartpoint = new TextPosition(startPos.line, 0);
            selEndpoint = new TextPosition(endPos.line, sourceLines[endPos.line].Length);
            UpdateSelectionDisplay();
        }

        public void ScrollIntoView(TextPosition position)
        {
            //float lineHeight = sourceCodeLinePrototype.rectTransform.sizeDelta.y;
            //Vector2 targetPosition = new Vector2(InsertionPointX(position.line, position.offset),
                //lineHeight * position.line);

            //var contentPanel = (transform as RectTransform);
            //Debug.Log("y: " + targetPosition.y);
            //Debug.Log("inv: " + scrollRect.transform.InverseTransformPoint(targetPosition).y);
            //Debug.Log("inv2: " + scrollRect.transform.InverseTransformPoint(transform.InverseTransformPoint(targetPosition)).y);
            //TODO
        }
        public void MoveCaretToPosition(TextPosition textPosition)
        {
            selStartpoint = textPosition;
            selEndpoint = textPosition;
            UpdateSelectionDisplay();
            ScrollIntoView(textPosition);
        }

        public string SelectedText()
        {
            TextPosition startPos = selEndFirst;
            TextPosition endPos = selEndLast;
            if (startPos.line == endPos.line)
            {
                //return sourceLines[startPos.line].Substring(startPos.offset, endPos.offset - startPos.offset);
                return sourceLines[startPos.line].GetString(startPos.offset, endPos.offset - startPos.offset);
            }
            else
            {
                _workingStringBuilder.Clear();
                //SourceL = sourceLines[startPos.line].GetString(startPos.offset);
                sourceLines[startPos.line].AppendToStringBuilder(_workingStringBuilder, startPos.offset);
                
                for (int i = startPos.line + 1; i < endPos.line; i++)
                {
                    _workingStringBuilder.Append('\n');
                    sourceLines[i].AppendToStringBuilder(_workingStringBuilder);
                    //result += "\n" + sourceLines[i];
                }
                //result += "\n" + sourceLines[endPos.line].Substring(0, endPos.offset);
                _workingStringBuilder.Append('\n');
                sourceLines[endPos.line].AppendToStringBuilder(_workingStringBuilder, 0, endPos.offset);
                return _workingStringBuilder.ToString();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Move selEndpoint by one character in either direction.
        /// </summary>
        /// <param name="dChar">+1 to advance to the right; -1 to retreat to the left</param>
        /// <returns>true if moved, false if hit a limit</returns>
        bool AdvanceOne(int dChar)
        {
            selEndpoint.offset += dChar;
            if (selEndpoint.offset < 0)
            {
                if (selEndpoint.line == 0)
                {
                    selEndpoint.offset = 0;
                    return false;
                }
                else
                {
                    selEndpoint.line--;
                    selEndpoint.offset = sourceLines[selEndpoint.line].Length;
                }
            }
            else if (selEndpoint.offset > sourceLines[selEndpoint.line].Length)
            {
                if (selEndpoint.line == sourceLines.Count - 1)
                {
                    selEndpoint.offset = sourceLines[selEndpoint.line].Length;
                    return false;
                }
                else
                {
                    selEndpoint.line++;
                    selEndpoint.offset = 0;
                }
            }
            return true;
        }

        void AdjustContentSize()
        {
            Vector2 size = new Vector2();
            float lineHeight = sourceCodeLinePrototype.rectTransform.sizeDelta.y;
            size.y = lineHeight * (sourceLines.Count + 1);
            size.x = _rectTransform.sizeDelta.x;

            // TODO we should resize the rect to support code that's large horizontally
            // it's just that as-is every extra character added when a line is past the
            // max length just causes a Layout rebuild, which is stupid slow.

            //size.x = _parentRectTransform.rect.width;
            //for(int i = 0; i < uiTexts.Count; i++)
            //{
            //var uiText = uiTexts[i];
            //size.x = Mathf.Max(size.x, uiText.rectTransform.sizeDelta.x + 20);
            //}
            //Debug.Log("Size was " + _rectTransform.sizeDelta);
            _rectTransform.sizeDelta = size;
            //Debug.Log("Now was " + _rectTransform.sizeDelta);
            _layoutElement.minHeight = size.y;
        }

        void ApplyUndo(UndoState state)
        {
            source = state.source;
            selStartpoint = state.selAnchor;
            selEndpoint = state.selEndpoint;
            UpdateSelectionDisplay();
        }

        void CaretOn()
        {
            if (!extendedSelection)
            {
                if (!interactable)
                    return;
                caret.enabled = true;
                caretOnTime = Time.realtimeSinceStartup;
            }
        }

        char CharAtPosition(TextPosition pos)
        {
            if (pos.offset >= sourceLines[pos.line].Length)
                return '\n';
            return sourceLines[pos.line].AtIndex(pos.offset);
            //return sourceLines[pos.line][pos.offset];
        }

        void DeleteBack()
        {
            if (extendedSelection)
                SetSelText("");
            else if (selStartpoint.line > 0 || selStartpoint.offset > 0)
            {
                var save = selStartpoint;
                // Special case: if we're at the start of the correct indentation for this line,
                // then delete the whole indentation and line break.  Otherwise, delete just
                // the character to the left (i.e. the standard case).
                SourceLine sourceLine = sourceLines[selStartpoint.line];
                if (selStartpoint.offset <= Indentation(ref sourceLine))
                {
                    MoveCursor(-1, 0, true);    // go to start of line
                    MoveCursor(-1, 0);          // go one more
                }
                else
                {
                    MoveCursor(-1, 0);
                }
                selStartpoint = save;
                SetSelText("");
            }
        }

        void DeleteForward()
        {
            if (extendedSelection)
                SetSelText("");
            else
            {
                var save = selStartpoint;
                MoveCursor(1, 0);
                //Debug.Log("DeleteForward moved cursor from " + save.offset + " to " + selStartpoint.offset);
                selStartpoint = save;
                //Debug.Log("Now extendedSelection=" + extendedSelection + ", from " + selStartpoint.offset + " to " + selEndpoint.offset);
                SetSelText("");
            }
        }

        void DeleteLine(int lineNum)
        {
            //Debug.Log("Removing line #" + lineNum);
            _isSourceDirty = true;
            SourceLine rmvLine = sourceLines[lineNum];
            SourceLine.ReturnCharArray(rmvLine.GetBackingArray());
            sourceLines.RemoveAt(lineNum);
            ReturnSourceCodePrefab(uiTexts[lineNum]);
            uiTexts.RemoveAt(lineNum);
        }

        void ExtendSelection(bool forward)
        {
            if (clickCount == 2)
            {
                // Extend selection to whole word (or quoted/parenthesized bit)
                if (!extendedSelection)
                {
                    SourceLine line = sourceLines[selStartpoint.line];
                    // Check for double-click on quotation mark, paren, or bracket; move anchor to other end
                    int pos = -1;
                    char charAtClick = char.MaxValue;
                    if(selStartpoint.offset > 0)
                        charAtClick = line.AtIndex(selStartpoint.offset - 1);

                    if (selStartpoint.offset > 0 && "()[]\"".IndexOf(charAtClick) >= 0)
                    {
                        pos = FindMatchingToken(ref line, selStartpoint.offset - 1);
                    }
                    else if (selStartpoint.offset < line.Length && selStartpoint.offset > 0 && "()[]\"".IndexOf(charAtClick) >= 0)
                    {
                        pos = FindMatchingToken(ref line, selStartpoint.offset);
                    }
                    else
                    {
                        // Didn't hit a quotation mark, paren, or bracket, so extend to whole word
                        selStartpoint.offset = FindWordStart(ref line, selStartpoint.offset);
                        selEndpoint.offset = FindWordEnd(ref line, selEndpoint.offset);
                    }
                    if (pos >= 0)
                    {
                        selStartpoint.offset = pos;
                        if (selStartpoint.offset > selEndpoint.offset)
                        {
                            // extend to include the parens/brackets/quotes, too
                            selStartpoint.offset++;
                            selEndpoint.offset--;
                        }
                    }
                }
                else
                {
                    // Once we already have an extended selection, then just grow it
                    // by words (don't try to get clever with quotes etc.).
                    SourceLine startLine = sourceLines[selStartpoint.line];
                    SourceLine endLine = sourceLines[selEndpoint.line];
                    if (forward)
                    {
                        selStartpoint.offset = FindWordStart(ref startLine, selStartpoint.offset);
                        selEndpoint.offset = FindWordEnd(ref endLine, selEndpoint.offset);
                    }
                    else
                    {
                        selStartpoint.offset = FindWordEnd(ref startLine, selStartpoint.offset);
                        selEndpoint.offset = FindWordStart(ref endLine, selEndpoint.offset);
                    }
                }
            }
            else if (clickCount > 2)
            {
                // Extend selection to whole lines
                if (forward)
                {
                    selStartpoint.offset = 0;
                    selEndpoint.offset = sourceLines[selEndpoint.line].Length;
                }
                else
                {
                    selStartpoint.offset = sourceLines[selStartpoint.line].Length;
                    selEndpoint.offset = 0;
                }
            }
        }

        /// <summary>
        /// Find the opening/closing token that matches the quotation
        /// mark, parenthesis, or square-bracket at the given position.
        /// </summary>
        /// <param name="line">source code line of interest</param>
        /// <param name="pos">position of a token to match</param>
        /// <returns>position of matching token, or -1 if not found</returns>
        int FindMatchingToken(ref SourceLine line, int pos)
        {
            char tok = line.AtIndex(pos);
            if (tok == '(' || tok == '[')
            {
                char closeTok = (tok == '(' ? ')' : ']');
                int depth = 1;
                while (pos + 1 < line.Length)
                {
                    pos++;
                    char posChar = line.AtIndex(pos);
                    if (posChar == tok)
                        depth++;
                    else if (posChar == closeTok)
                    {
                        depth--;
                        Debug.Log("found " + closeTok + " at " + pos + "; depth now " + depth);
                        if (depth == 0) return pos;
                    }
                }
            }
            else if (tok == ')' || tok == ']')
            {
                char openTok = (tok == ')' ? '(' : '[');
                int depth = 1;
                while (pos > 0)
                {
                    pos--;
                    char posChar = line.AtIndex(pos);
                    if (posChar == tok)
                        depth++;
                    else if (posChar == openTok)
                    {
                        depth--;
                        Debug.Log("found " + openTok + " at " + pos + "; depth now " + depth);
                        if (depth == 0) return pos;
                    }
                }
            }
            else if (tok == '"')
            {
                // Quotes are different.  We can't tell openers from closers without
                // simply counting the start of the string.
                bool quoteOpen = false;
                int lastQuote = -1;
                for (int i = 0; i < line.Length; i++)
                {
                    if (line.AtIndex(i) == '"')
                    {
                        if (i + 1 < line.Length && line.AtIndex(i + 1) == '"')
                        {
                            // double-double quote... ignore
                            i++;
                            continue;
                        }
                        if (i == pos && quoteOpen) return lastQuote;
                        if (i > pos && quoteOpen) return i;
                        quoteOpen = !quoteOpen;
                        lastQuote = i;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// Find the start of the word at or before the given position.
        /// </summary>
        int FindWordStart(ref SourceLine line, int pos)
        {
            while (pos > 0)
            {
                char c = line.AtIndex(pos - 1);
                if (!Lexer.IsIdentifier(c))
                    break;
                pos--;
            }
            return pos;
        }

        /// <summary>
        /// Find the end of the word at or before the given position.
        /// </summary>
        int FindWordEnd(ref SourceLine line, int pos)
        {
            while (pos < line.Length)
            {
                char c = line.AtIndex(pos);
                if (!Lexer.IsIdentifier(c))
                    break;
                pos++;
            }
            return pos;
        }

        UndoState GetUndoState()
        {
            var undo = new UndoState();
            undo.source = source;
            undo.selAnchor = selStartpoint;
            undo.selEndpoint = selEndpoint;
            return undo;
        }

        /// <summary>
        /// Return the position between two characters on the given line.
        /// </summary>
        /// <param name="uiText">Text or TextMeshProUGUI</param>
        /// <param name="charOffset">how many characters to the left of the insertion point</param>
        /// <returns>X position between the indicated characters</returns>
        float InsertionPointX(int lineNum, int charOffset)
        {
            TextMeshProUGUI tmPro = uiTexts[lineNum];
            //SourceLine sourceLine = sourceLines[lineNum];
            float x = tmPro.rectTransform.anchoredPosition.x;
            float x0 = 0;
            TMP_TextInfo textInfo = tmPro.textInfo;
            TMP_CharacterInfo[] charInfo = textInfo.characterInfo;
            int textLen = textInfo.characterCount;
            if (charOffset > textLen)
                charOffset = textLen;
            if (charOffset > 0)
                x0 = charInfo[charOffset - 1].xAdvance;
            x += x0;
            return x;
        }

        /// <summary>
        /// Count how many tabs are at the beginning of the given source line.
        /// </summary>
        int Indentation(ref SourceLine sourceLine)
        {
            for (int i = 0; i < sourceLine.Length; i++)
            {
                if (sourceLine.AtIndex(i) != '\t')
                    return i;
            }
            // Found nothing at all but tabs?  Still counts as indented.
            return sourceLine.Length;
        }

        /// <summary>
        /// Figure out how the given source line affects indentation.
        /// </summary>
        /// <param name="sourceLine">line of code</param>
        /// <param name="outdentThis">0, or 1 if this line should outdent relative to the previous line</param>
        /// <param name="indentNext">how much the next line should be indented relative to this one</param>
        void IndentEffect(ref SourceLine sourceLine, out int outdentThis, out int indentNext)
        {
            indentNext = outdentThis = 0;
            var lexer = new Lexer(sourceLine);
            while (!lexer.AtEnd)
            {
                try
                {
                    Token tok = lexer.Dequeue();
                    if (tok.type == Token.Type.Keyword)
                    {
                        if (tok.text == "if")
                        {
                            // Tricky case, because of single-line 'if'.
                            // We can recognize that by having more non-comment tokens
                            // right after the 'then' keyword.
                            while (!lexer.AtEnd && lexer.Peek().type != Token.Type.EOL)
                            {
                                tok = lexer.Dequeue();
                                if (tok.type == Token.Type.Keyword && tok.text == "then")
                                {
                                    // OK, we got a "then", so if the next token is EOL, then
                                    // we need to indent.  If it's anything else, we don't.
                                    if (lexer.Peek().type == Token.Type.EOL) indentNext++;
                                    break;
                                }
                            }
                        }
                        else if (tok.text == "else" || tok.text == "else if")
                        {
                            outdentThis++;
                            indentNext++;
                        }
                        else if (tok.text == "while" || tok.text == "for" || tok.text == "function")
                        {
                            indentNext++;
                        }
                        else if (tok.text.StartsWith("end"))
                        {
                            if (indentNext > 0) indentNext--;
                            else outdentThis++;
                        }
                    }
                }
                catch (LexerException)
                {
                }
            }
        }

        /// <summary>
        /// Find the default "end" keyword to match the most recent block
        /// opener before the selection point.  This is used for the shift-Return
        /// function that automatically inserts the appropriate closer.
        /// </summary>
        /// <returns>end string (e.g. "end while"), or null</returns>
        string FindDefaultEnder()
        {
            int line = selStartpoint.line - 1;
            var closers = new Queue<string>();
            while (line >= 0)
            {
                Lexer lexer = new Lexer(sourceLines[line]);
                string ender = null;
                bool onIfStatement = false;
                while (!lexer.AtEnd)
                {
                    try
                    {
                        var tok = lexer.Dequeue();
                        if (tok.type != Token.Type.Keyword) continue;
                        if (tok.text.StartsWith("end "))
                        {
                            closers.Enqueue(tok.text);
                        }
                        else if (tok.text == "while")
                        {
                            onIfStatement = false;
                            if (closers.Count > 0 && closers.Peek() == "end while")
                            {
                                closers.Dequeue();
                            }
                            else
                            {
                                return "end while";
                            }
                        }
                        else if (tok.text == "for")
                        {
                            onIfStatement = false;
                            if (closers.Count > 0 && closers.Peek() == "end for")
                            {
                                closers.Dequeue();
                            }
                            else
                            {
                                return "end for";
                            }
                        }
                        else if (tok.text == "function")
                        {
                            onIfStatement = false;
                            if (closers.Count > 0 && closers.Peek() == "end function")
                            {
                                closers.Dequeue();
                            }
                            else
                            {
                                return "end function";
                            }
                        }
                        else if (tok.text == "if")
                        {
                            onIfStatement = true;
                        }
                        else if (tok.text == "then" && onIfStatement)
                        {
                            // If there is any token after `then` besides EOL,
                            // then this is a single-line 'if' and doesn't need a closer.
                            // But if it's followed by EOL, then it's a block `if`.
                            if (lexer.Peek().type == Token.Type.EOL)
                            {
                                if (closers.Count > 0 && closers.Peek() == "end if")
                                {
                                    closers.Dequeue();
                                }
                                else
                                {
                                    return "end if";
                                }
                            }
                        }
                        else if (tok.text == "else" || tok.text == "else if")
                        {
                            onIfStatement = false;
                            if (closers.Count > 0 && closers.Peek() == "end if")
                            {
                                // Don't dequeue the end-if, as that still applies!
                            }
                            else
                            {
                                return "end if";
                            }
                        }
                    }
                    catch (LexerException)
                    {

                    }
                }
                if (ender != null) return ender;
                line--;
            }
            return null;
        }

        public void InsertLine(int lineNum, ref SourceLine lineText)
        {
            //Debug.Log("Inserting line #" + lineNum + " total " + (uiTexts.Count + 1));
            sourceLines.Insert(lineNum, lineText);

            var newUIText = GetSourceCodePrefab();
            var rt = newUIText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            SetText(newUIText, lineText);
            uiTexts.Insert(lineNum, newUIText);
            newUIText.gameObject.SetActive(true);
        }

        /// <summary>
        /// Return whether the given character is a symbol character, i.e.,
        /// not a normal letter. Used for moving by letter
        /// </summary>
        static bool IsSymbolChar(char c)
        {
            if (c <= '/')
                return true;
            if (c <= '9')
                return false;
            if (c <= '@')
                return true;
            if (c <= 'Z')
                return false;
            if (c <= '`')
                return true;
            if (c <= 'z')
                return false;
            return true;
        }

        bool KeyPressedOrRepeats(KeyCode keyCode)
        {
            if (Input.GetKeyDown(keyCode))
            {
                keyRepeatTime[keyCode] = Time.realtimeSinceStartup + keyRepeatDelay;
                return true;
            }
            else if (Input.GetKey(keyCode))
            {
                if (Time.realtimeSinceStartup > keyRepeatTime[keyCode])
                {
                    keyRepeatTime[keyCode] = Time.realtimeSinceStartup + keyRepeatInterval;
                    return true;
                }
            }
            return false;
        }

        void LoadSource(string sourceCode)
        {
            _isSourceDirty = true;
            int lineNum = 0;
            int i = 0;
            while(i < sourceCode.Length)
            {
                //_workingStringBuilder.Clear();
                SourceLine sourceLine = new SourceLine(SourceLine.GetCharArray(SourceLineDefaultCapacity));

                // Pull out a line
                for(; i < sourceCode.Length; i++)
                {
                    char c = sourceCode[i];
                    if (c == '\n'
                        || c == '\r')
                    {
                        i++;
                        // Check if we have a \r\n, which happens in some windows formats
                        if (c == '\r' && i < sourceCode.Length && sourceCode[i] == '\n')
                            i++;
                        break;
                    }
                    sourceLine.Append(c);
                    //_workingStringBuilder.Append(c);
                }
                //string lineText = _workingStringBuilder.ToString();
                if (lineNum >= sourceLines.Count)
                {
                    InsertLine(lineNum, ref sourceLine);
                }
                else
                {
                    SourceLine.ReturnCharArray(sourceLines[lineNum].GetBackingArray());
                    sourceLines[lineNum] = sourceLine;
                    SetText(uiTexts[lineNum], sourceLine);
                }
                lineNum++;
            }
            // Make sure we add at least one line
            if(i == 0)
            {
                SourceLine sourceLine = new SourceLine(SourceLine.GetCharArray(SourceLineDefaultCapacity));
                if (lineNum >= sourceLines.Count)
                {
                    InsertLine(lineNum, ref sourceLine);
                }
                else
                {
                    SourceLine.ReturnCharArray(sourceLines[lineNum].GetBackingArray());
                    sourceLines[lineNum] = sourceLine;
                    SetText(uiTexts[lineNum], sourceLine);
                }
                lineNum++;
            }

            while (sourceLines.Count > lineNum)
                DeleteLine(sourceLines.Count - 1);

            UpdateLineYPositions();

            int maxLine = Max(sourceLines.Count - 1, 0);
            selStartpoint.line = Min(selStartpoint.line, maxLine);
            selStartpoint.offset = Min(selStartpoint.offset, selStartpoint.line < sourceLines.Count ? sourceLines[selStartpoint.line].Length : 0);
            selEndpoint.line = Min(selEndpoint.line, maxLine);
            selEndpoint.offset = Min(selEndpoint.offset, selEndpoint.line < sourceLines.Count ? sourceLines[selEndpoint.line].Length : 0);
            UpdateSelectionDisplay();
        }

        static int Max(int a, int b)
        {
            return a > b ? a : b;
        }

        static int Min(int a, int b)
        {
            return a < b ? a : b;
        }

        /// <summary>
        /// Update the selection according to the given keyboard input.
        /// </summary>
        /// <param name="dChar">left/right key input</param>
        /// <param name="dLine">up/down key input</param>
        /// <param name="allTheWay">go to start/end of line or document</param>
        void MoveCursor(int dChar, int dLine, bool allTheWay = false)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (extendedSelection && !shift)
            {
                if (dChar != 0)
                {
                    // Collapse the selection towards whichever end is in the direction we're moving, and return.
                    if (dChar > 0) selStartpoint = selEndpoint = selEndLast;
                    else selStartpoint = selEndpoint = selEndFirst;
                    UpdateSelectionDisplay();
                    preferredX = caret.rectTransform.anchoredPosition.x;
                    CaretOn();
                    return;
                }
                else
                {
                    // Collapse the selection towards the indicated end, and then process the up/down key
                    if (dLine > 0) selStartpoint = selEndpoint = selEndLast;
                    else selStartpoint = selEndpoint = selEndFirst;
                }
            }
            if (dChar != 0)
            {
                //bool byWord = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                bool byWord = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (allTheWay)
                {
                    // Go to start/end of line.
                    if (dChar < 0) selEndpoint.offset = 0;
                    else selEndpoint.offset = sourceLines[selEndpoint.line].Length;
                }
                else if (byWord)
                {
                    // skip any nonword characters; then advance till we get to nonword chars again
                    bool isInSymbols = false;
                    if (dChar < 0) AdvanceOne(-1);
                    char c = CharAtPosition(selEndpoint);
                    // Clear all leading spaces
                    while (c == ' ' && AdvanceOne(dChar))
                        c = CharAtPosition(selEndpoint);
                    isInSymbols = IsSymbolChar(c);
                    bool isFirstLoop = true;
                    // Keep moving until
                    // 1) We hit a new line
                    // 2) We're on letters and hit a non-letter
                    // 3) We're on symbols and hit a non-symbol
                    // If we hit a new line, then we check to see if the next char
                    // is a tab, and move over it if so
                    while (isInSymbols == IsSymbolChar(c)
                        && AdvanceOne(dChar))
                    {
                        c = CharAtPosition(selEndpoint);
                        //Debug.Log("dchar " + dChar + "char: " + c);
                        if(c == '\n')
                        {
                            // If we've already seen characters, then
                            // we stop at the new line. Otherwise, we
                            // pass through it
                            if (isFirstLoop)
                            {
                                AdvanceOne(dChar);
                                char newChar = CharAtPosition(selEndpoint);
                                //Debug.Log("Advanced, c is \"" + newChar + "\" or " + (int)newChar);
                                if (newChar == '\n' || newChar == '\t')
                                {
                                    AdvanceOne(dChar);
                                    //Debug.Log("Advanced again, now c is " + CharAtPosition(selEndpoint));
                                }
                            }
                            break;
                        }
                        isFirstLoop = false;
                    }
                    if (dChar < 0) AdvanceOne(1);
                }
                else AdvanceOne(dChar);
            }
            if (dLine < 0)
            {
                if (allTheWay)
                {
                    // Go to start of document
                    selEndpoint.line = 0;
                    selEndpoint.offset = 0;
                }
                else if (selEndpoint.line == 0)
                {
                    // Up-arrow on first line: jump to start of line (Mac thing, but cool everywhere)
                    selEndpoint.offset = 0;
                }
                else
                {
                    selEndpoint.line--;
                    selEndpoint.offset = OffsetForXPosition(uiTexts[selEndpoint.line], preferredX);
                }
            }
            else if (dLine > 0)
            {
                if (allTheWay)
                {
                    // Go to end of document
                    selEndpoint.line = sourceLines.Count - 1;
                    selEndpoint.offset = sourceLines[selEndpoint.line].Length;
                }
                else if (selEndpoint.line == sourceLines.Count - 1)
                {
                    // Down-arrow on last line: jump to end of line
                    selEndpoint.offset = sourceLines[selEndpoint.line].Length;
                }
                else
                {
                    selEndpoint.line++;
                    selEndpoint.offset = OffsetForXPosition(uiTexts[selEndpoint.line], preferredX);
                }
            }
            if (!shift)
            {
                // Without shift key held, move the caret, rather than extending the selection.
                selStartpoint = selEndpoint;
            }

            UpdateSelectionDisplay();
            if (dChar != 0) preferredX = caret.rectTransform.anchoredPosition.x;
            CaretOn();
            ScrollIntoView(selEndpoint);
        }

        /// <summary>
        /// Find number of characters to the left of the given X position in the given line.
        /// </summary>
        /// <param name="uiText">Text or TextMeshProUGUI</param>
        /// <param name="x">x offset relative to parent container</param>
        /// <returns>character offset within that line</returns>
        int OffsetForXPosition(TextMeshProUGUI tmPro, float x)
        {
            int result = 0;
            x -= sourceCodeLinePrototype.rectTransform.anchoredPosition.x;
            TMP_TextInfo textInfo = tmPro.textInfo;
            TMP_CharacterInfo[] charInfo = textInfo.characterInfo;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                //float midPoint = (charInfo[i].bottomLeft.x + charInfo[i].bottomRight.x) / 2;
                float midPoint = (charInfo[i].bottomLeft.x + charInfo[i].xAdvance) / 2;
                if (midPoint > x) break;
                result++;
            }
            return result;
        }

        TextPosition PositionAtXY(Vector2 xy)
        {
            int lineNum = Mathf.FloorToInt(Mathf.Abs(xy.y) / sourceCodeLinePrototype.rectTransform.sizeDelta.y);
            if (lineNum < 0) return new TextPosition(0, 0);
            if (lineNum >= sourceLines.Count) return new TextPosition(sourceLines.Count - 1, sourceLines[sourceLines.Count - 1].Length);
            return new TextPosition(lineNum, Min(sourceLines[lineNum].Length, OffsetForXPosition(uiTexts[lineNum], xy.x)));
        }

        void ProcessKeys()
        {
            if (!hasFocus || !Input.anyKey) return; // quick bail-out

            bool cmd = Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (KeyPressedOrRepeats(KeyCode.LeftArrow)) MoveCursor(-1, 0, cmd);
            if (KeyPressedOrRepeats(KeyCode.RightArrow)) MoveCursor(1, 0, cmd);
            if (KeyPressedOrRepeats(KeyCode.UpArrow)) MoveCursor(0, -1, cmd);
            if (KeyPressedOrRepeats(KeyCode.DownArrow)) MoveCursor(0, 1, cmd);
            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (ctrl) MoveCursor(0, -1, true);
                else MoveCursor(-1, 0, true);
            }
            if (Input.GetKeyDown(KeyCode.End))
            {
                if (ctrl) MoveCursor(0, 1, true);
                else MoveCursor(1, 0, true);
            }

            if (KeyPressedOrRepeats(KeyCode.Backspace)) DeleteBack();
            if (KeyPressedOrRepeats(KeyCode.Delete)) DeleteForward();

            if (cmd || ctrl)
            {
                // Process keyboard shortcuts
                if (Input.GetKeyDown(KeyCode.X)) EditCut();
                if (Input.GetKeyDown(KeyCode.C)) EditCopy();
                if (Input.GetKeyDown(KeyCode.V)) EditPaste();
                if (Input.GetKeyDown(KeyCode.A)) EditSelectAll();
                if (Input.GetKeyDown(KeyCode.LeftBracket)) Indent(-1);
                if (Input.GetKeyDown(KeyCode.RightBracket)) Indent(1);
                if (Input.GetKeyDown(KeyCode.Z))
                {
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) EditRedo();
                    else EditUndo();
                }
            }
            else
            {
                // Processing of normal text input is now done in OnGUI.
            }
        }

        /// <summary>
        /// Change the number of leading tabs to the given number.
        /// </summary>
        void Reindent(ref SourceLine sourceLine, int indentation)
        {
            int i;
            for (i = 0; i < sourceLine.Length; i++)
            {
                //if (!Lexer.IsWhitespace(sourceLine.AtIndex(i)))
                if (sourceLine.AtIndex(i) != '\t')
                    break;
            }
            // Clamp indentation
            if (indentation < 0)
                indentation = 0;
            else if (indentation > 16)
                indentation = 16;

            int numTabsToAdd = indentation - i;
            if (numTabsToAdd == 0)
                return;
            // Removing tabs
            if (numTabsToAdd < 0)
            {
                sourceLine.TrimStart(-numTabsToAdd);
            }
            else
            {
                // Adding tabs
                sourceLine.Prepend('\t', numTabsToAdd);
            }
        }

        /// <summary>
        /// Reindent the given range of source lines.
        /// </summary>
        void ReindentLines(int fromLine, int toLine)
        {
            //Debug.Log("Reindenting lines " + fromLine + " to " + toLine);
            int outdentThis = 0, indentNext = 0;
            int indent = 0;
            if (fromLine > 0)
            {
                SourceLine prevLine = sourceLines[fromLine - 1];
                IndentEffect(ref prevLine, out outdentThis, out indentNext);
                indent = Indentation(ref prevLine) + indentNext;
            }
            for (int lineNum = fromLine; lineNum <= toLine; lineNum++)
            {
                SourceLine line = sourceLines[lineNum];
                int curIndent = Indentation(ref line);
                IndentEffect(ref line, out outdentThis, out indentNext);
                indent = indent - outdentThis;
                if (curIndent != indent)
                {
                    Reindent(ref line, indent);
                    UpdateLine(lineNum, ref line);
                    if (lineNum == selStartpoint.line) selStartpoint.offset += indent - curIndent;
                    if (lineNum == selEndpoint.line) selEndpoint.offset += indent - curIndent;
                }
                indent += indentNext;
            }
        }

        /// <summary>
        /// If we have an extended selection, replace it with the given text.
        /// Otherwise, insert the given text at the caret position.
        /// Also, update the undo/redo stack.
        /// </summary>
        /// <param name="s"></param>
        void SetSelText(string s)
        {
            s = s.Replace("\r\n", "\n");
            s = s.Replace("\r", "\n");

            bool linesInsertedOrRemoved = false;

            if (Time.realtimeSinceStartup - lastEditTime > 1)
            {
                // This is a new edit.  Make sure we can undo to the previous state.
                // TODO
                //StoreUndo();
            }
            else
            {
                // This is a continuation of the previous edit.  No need to store a new undo state.
            }
            lastEditTime = Time.realtimeSinceStartup;

            // Start by deleting the current extended selection, if any
            if (extendedSelection)
            {
                if (selEndpoint.line == selStartpoint.line)
                {
                    // Easy case: selection is all on one line
                    SourceLine line = sourceLines[selStartpoint.line];
                    int startPos = Min(selStartpoint.offset, selEndpoint.offset);
                    int endPos = Max(selStartpoint.offset, selEndpoint.offset);
                    try
                    {
                        line.TrimMiddle(startPos, endPos);
                        //line = line.Substring(0, startPos) + line.Substring(endPos);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError("Got " + e + " while trying to cut from " + startPos + " to " + endPos + " in line " + line.Length + " long");
                    }
                    //Debug.Log("Cut from " + startPos + " to " + endPos + ", leaving " + line);
                    UpdateLine(selStartpoint.line, ref line);
                    selStartpoint.offset = startPos;
                }
                else
                {
                    // Harder case: multi-line selection.
                    TextPosition startPos = selEndFirst;
                    TextPosition endPos = selEndLast;
                    // First, combine the end line with the start line and the new text.
                    SourceLine line = new SourceLine(SourceLine.GetCharArray(SourceLineDefaultCapacity));
                    try
                    {
                        SourceLine startLine = sourceLines[startPos.line];
                        SourceLine endLine = sourceLines[endPos.line];

                        SourceLine.MergeSourceLines(ref line, ref startLine, 0, startPos.offset, ref endLine, endPos.offset, endLine.Length - endPos.offset);
                        //line = sourceLines[startPos.line].Substring(0, startPos.offset)
                            //+ sourceLines[endPos.line].Substring(endPos.offset);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError("Got " + e + " while trying to cut from " + startPos
                            + " to " + endPos + " in lines "
                            + sourceLines[startPos.line].Length + " and "
                            + sourceLines[endPos.line].Length + " long");

                    }
                    UpdateLine(startPos.line, ref line);
                    selStartpoint = startPos;
                    // Then, delete the middle-to-end lines entirely.
                    for (int i = endPos.line; i > startPos.line; i--)
                    {
                        DeleteLine(i);
                        endPos.line--;
                    }
                    linesInsertedOrRemoved = true;
                }
                selEndpoint = selStartpoint;
            }

            // Then, insert the given text (which may be multiple lines)
            int reindentFrom = selStartpoint.line;
            int reindentTo = selStartpoint.line;
            if (!string.IsNullOrEmpty(s))
            {
                string[] lines = s.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (i < lines.Length - 1)
                    {
                        // Insert this line, followed by a line break
                        //string src = sourceLines[selStartpoint.line].Substring(0, selStartpoint.offset) + lines[i];
                        //string nextSrc = sourceLines[selStartpoint.line].Substring(selStartpoint.offset);
                        SourceLine line = sourceLines[selStartpoint.line];
                        SourceLine nextLine = new SourceLine(SourceLine.GetCharArray(SourceLineDefaultCapacity));
                        SourceLine.SubstringExisting(ref nextLine, ref line, selStartpoint.offset, line.Length - selStartpoint.offset);
                        line.TrimEnd(line.Length - selStartpoint.offset);
                        line.Append(lines[i]);
                        //line = line.InsertMiddle(selStartpoint.offset)
                        UpdateLine(selStartpoint.line, ref line);
                        InsertLine(selStartpoint.line + 1, ref nextLine);
                        selStartpoint.line++;
                        selStartpoint.offset = 0;
                        linesInsertedOrRemoved = true;
                        // ...and then strip leading whitespace from the next line we're going to paste
                        lines[i + 1] = lines[i + 1].TrimStart();
                    }
                    else
                    {
                        // Insert this text without a line break
                        SourceLine src = sourceLines[selStartpoint.line];
                        if (selStartpoint.offset > src.Length)
                            selStartpoint.offset = src.Length;

                        src.InsertMiddle(selStartpoint.offset, lines[i], selStartpoint.offset);
                        //src = src.Substring(0, selStartpoint.offset)
                            //+ lines[i] + src.Substring(selStartpoint.offset);
                        UpdateLine(selStartpoint.line, ref src);
                        selStartpoint.offset += lines[i].Length;
                    }
                    selEndpoint = selStartpoint;
                }
            }

            // Reindent lines in the affected range.
            reindentTo = sourceLines.Count - 1;     // ToDo: something smarter here!
            ReindentLines(reindentFrom, reindentTo);

            // Adjust line Y positions if needed
            if (linesInsertedOrRemoved)
                UpdateLineYPositions();

            // And we're just about done.
            AdjustContentSize();
            UpdateSelectionDisplay();
            preferredX = caret.rectTransform.anchoredPosition.x;
            CaretOn();
        }

        /// <summary>
        /// Set the text of the given Text or TextMeshProUGUI.
        /// </summary>
        void SetText(TextMeshProUGUI tmPro, SourceLine sourceLine, bool applyMarkup = true, bool sizeToFit = true)
        {
#if UNITY_EDITOR // micro optimization, no need to change names in build
            tmPro.gameObject.name = sourceLine.GetString();
#endif

            // If we have a source without any characters, then TMP won't render it correctly
            // so we just use SetText, which doesn't seem to have the same bug
            if(sourceLine.Length > 0)
            {
                bool useMarkedUp = applyMarkup && style != null;
                SourceLine markedUp;
                if (useMarkedUp)
                {
                    style.Markup(ref _workingMarkupSourceLine, sourceLine);
                    markedUp = _workingMarkupSourceLine;
                }
                else
                    markedUp = sourceLine;
                tmPro.SetCharArray(markedUp.GetBackingArray(), markedUp.StartIdx, markedUp.Length);
            }
            else
            {
                //tmPro.SetText((string)null);
                tmPro.SetText(string.Empty);
            }

            if (sizeToFit)
            {
                tmPro.ForceMeshUpdate();
                tmPro.rectTransform.sizeDelta = new Vector2(tmPro.preferredWidth + 40, tmPro.rectTransform.sizeDelta.y);
            }
        }

        void StoreUndo()
        {
            var undo = GetUndoState();

            undoPosition++;
            if (undoPosition >= undoStack.Count)
            {
                undoStack.Add(undo);
                if (undoStack.Count > undoLimit) undoStack.RemoveAt(0);
            }
            else
            {
                undoStack[undoPosition] = undo;
                if (undoPosition + 1 < undoStack.Count)
                {
                    undoStack.RemoveRange(undoPosition + 1, undoStack.Count - (undoPosition + 1));
                }
            }
        }

        /// <summary>
        /// Update the text of the given line, as it appears in the UI.
        /// </summary>
        void UpdateLine(int lineNum, ref SourceLine sourceLine)
        {
            _isSourceDirty = true;
            // Return the existing, if it's different
            SourceLine existing = sourceLines[lineNum];
            if (existing.GetBackingArray() != sourceLine.GetBackingArray())
                SourceLine.ReturnCharArray(existing.GetBackingArray());

            sourceLines[lineNum] = sourceLine;
            SetText(uiTexts[lineNum], sourceLine);
        }

        /// <summary>
        /// Update the Y positions of our source lines, and also update line
        /// numbers if we have them.
        /// </summary>
        public void UpdateLineYPositions()
        {
            float y = 0;
            float dy = sourceCodeLinePrototype.rectTransform.sizeDelta.y;
            for (int i = 0; i < uiTexts.Count; i++)
            {
                uiTexts[i].rectTransform.anchoredPosition = new Vector2(
                    uiTexts[i].rectTransform.anchoredPosition.x, y);

                if (lineNumberPrototype != null)
                {
                    if (i >= lineNums.Count)
                    {
                        var lineNumObj = GetLineNumberPrefab();
                        _workingLineNumber.Reset();
                        _workingLineNumber.AppendNumber(i + lineNumbersStartAt);
                        SetText(lineNumObj, _workingLineNumber, false, false);
                        //SetText(noob, (i + lineNumbersStartAt).ToString(lineNumberFormatString), false, false);
                        lineNums.Add(lineNumObj);
                    }
                    lineNums[i].rectTransform.anchoredPosition = new Vector2(
                        lineNums[i].rectTransform.anchoredPosition.x, y);
                    lineNums[i].gameObject.SetActive(true);
                }

                y -= dy;
            }

            for (int i = uiTexts.Count; i < lineNums.Count; i++)
            {
                lineNums[i].gameObject.SetActive(false);
            }

            AdjustContentSize();
        }

        /// <summary>
        /// Update the visual display of the selection (the caret or selection highlight).
        /// </summary>
        public void UpdateSelectionDisplay()
        {
            //Debug.Log("Update selection display endpoint line: " + selEndpoint.line + " selOffset: " + selEndpoint.offset);
            caret.rectTransform.anchoredPosition = new Vector2(
                InsertionPointX(selEndpoint.line, selEndpoint.offset) + caret.rectTransform.sizeDelta.x / 2f,
                //InsertionPointX(selEndpoint.line, selEndpoint.offset),
                uiTexts[selEndpoint.line].rectTransform.anchoredPosition.y);

            if (extendedSelection)
            {
                TextPosition startPos = selEndFirst;
                TextPosition endPos = selEndLast;
                float left = sourceCodeLinePrototype.rectTransform.anchoredPosition.x - 4;
                // First partial line
                float x0 = InsertionPointX(startPos.line, startPos.offset);
                float x1 = _rectTransform.rect.width;
                if (endPos.line == startPos.line) x1 = InsertionPointX(endPos.line, endPos.offset);
                var rt = selectionHighlight.rectTransform;
                rt.anchoredPosition = new Vector2(
                    x0, uiTexts[startPos.line].rectTransform.anchoredPosition.y);
                rt.sizeDelta = new Vector2(x1 - x0, uiTexts[startPos.line].rectTransform.sizeDelta.y);
                selectionHighlight.gameObject.SetActive(true);

                if (endPos.line > startPos.line)
                {
                    // Last partial line
                    x0 = left;
                    x1 = InsertionPointX(endPos.line, endPos.offset);
                    rt = selHighlight2.rectTransform;
                    rt.anchoredPosition = new Vector2(
                        x0, uiTexts[endPos.line].rectTransform.anchoredPosition.y);
                    rt.sizeDelta = new Vector2(x1 - x0, uiTexts[endPos.line].rectTransform.sizeDelta.y);
                    selHighlight2.gameObject.SetActive(true);
                }
                else selHighlight2.gameObject.SetActive(false);

                if (endPos.line > startPos.line + 1)
                {
                    // Middle full line(s)
                    x0 = left;
                    x1 = _rectTransform.rect.width;
                    float y0 = uiTexts[startPos.line + 1].rectTransform.anchoredPosition.y;
                    float y1 = uiTexts[endPos.line].rectTransform.anchoredPosition.y;
                    rt = selHighlightMid.rectTransform;
                    rt.anchoredPosition = new Vector2(x0, y0);
                    rt.sizeDelta = new Vector2(x1 - x0, Mathf.Abs(y1 - y0));
                    selHighlightMid.gameObject.SetActive(true);
                }
                else selHighlightMid.gameObject.SetActive(false);

                caret.enabled = false;
            }
            else
            {
                // No extended selection: just show the caret
                caret.rectTransform.SetAsLastSibling();
                if (isFocused)
                    CaretOn();
                selectionHighlight.gameObject.SetActive(false);
                selHighlight2.gameObject.SetActive(false);
                selHighlightMid.gameObject.SetActive(false);
            }
        }

        private static float WidthInFont(string str, Font font, int fontSize)
        {
            float totalWidth = 0;
            foreach (char c in str.ToCharArray())
            {
                CharacterInfo characterInfo;
                font.GetCharacterInfo(c, out characterInfo, fontSize);
                totalWidth += characterInfo.advance;
            }

            return totalWidth;
        }
        #endregion
        protected void OnGUI()
        {
            if (!hasFocus) return;
            Event e = Event.current;
#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
		if (e.isKey && e.type == EventType.KeyDown && e.character > 0) {
#else
            if (e.isKey && e.keyCode == KeyCode.None)
            {
#endif
                try
                {
                    char c = e.character;
                    if (c == '\n' && e.shift)
                    {
                        SetSelText("\n");
                        string ender = FindDefaultEnder();
                        if (ender != null)
                        {
                            var pos = selStartpoint;
                            SetSelText("\n" + ender);
                            selStartpoint = selEndpoint = pos;
                            UpdateSelectionDisplay();
                        }
                    }
                    else
                    {
                        //Debug.Log("Current state: " + sourceLines.Count + " lines, selStartpoint=" + selStartpoint);
                        //Debug.Log("Setting selText text to " + c + "  (" + (int)c + ")");
                        SetSelText(c.ToString());
                    }
                    ScrollIntoView(selEndpoint);
                } catch(Exception ex)
                {
                    Debug.LogError(ex.ToString());
                }
            }
        }
        protected void Update()
        {
            if (!hasFocus)
                return;

            ProcessKeys();

            if (!extendedSelection)
            {
                // blink the caret
                if (Time.realtimeSinceStartup > caretOnTime + 0.7)
                {
                    caret.enabled = false;
                    if (Time.realtimeSinceStartup > caretOnTime + 1f) CaretOn();
                }
            }
        }
    }
}