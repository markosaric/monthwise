import ts from 'typescript'

/**
 * Every text the app shows comes from a texts file, in English and Croatian (croatian-language.md
 * FR-9, NFR-1, acceptance criterion 9). This test reads the screens' source and fails on text written
 * straight into a component: JSX text, text in the attributes people read or hear, toasts, and
 * template strings shown in the page. Texts files themselves are skipped; TypeScript checks that
 * their Croatian half has every text of the English one.
 */

/** Every source file under src, keyed by its path relative to src (read by Vite at test time). */
const sources = Object.fromEntries(
  Object.entries(
    import.meta.glob<string>('../**/*.{ts,tsx}', { query: '?raw', import: 'default', eager: true }),
  ).map(([file, text]) => [file.replace(/^\.\.\//, ''), text]),
)

/** Texts that are the same in every language: the app's name and its logo letter. */
const sameInEveryLanguage = new Set(['Monthwise', 'M', '—', '·', '€', '%'])

/** Attributes whose text people read or hear. */
const shownAttributes = new Set([
  'title',
  'label',
  'description',
  'placeholder',
  'aria-label',
  'alt',
  'confirmLabel',
  'detail',
  'emptyText',
])

/** Attributes that hold code, not text: an id, a route, a CSS class. */
const codeAttributes = new Set([
  'className',
  'key',
  'to',
  'href',
  'id',
  'htmlFor',
  'aria-describedby',
  'aria-labelledby',
  'aria-controls',
  'style',
])

function sourceFiles(): string[] {
  return Object.keys(sources).filter(
    (file) =>
      !/^test\//.test(file) &&
      !/\.(test|d)\.tsx?$/.test(file) &&
      !/(^|\/)texts(\.hr)?\.ts$/.test(file),
  )
}

const hasWords = (text: string) => /[A-Za-zčćđšžČĆĐŠŽ]{2,}/.test(text)

function staticText(node: ts.TemplateExpression): string {
  return [node.head.text, ...node.templateSpans.map((s) => s.literal.text)].join(' ')
}

/** Whether an expression is shown as JSX content, e.g. `{done ? 'Paid' : ''}` or `{a && 'text'}`. */
function inJsxContent(node: ts.Node): boolean {
  const joins = [
    ts.SyntaxKind.AmpersandAmpersandToken,
    ts.SyntaxKind.BarBarToken,
    ts.SyntaxKind.QuestionQuestionToken,
    ts.SyntaxKind.PlusToken,
  ]
  let current = node.parent
  while (
    (ts.isConditionalExpression(current) && current.condition !== node) ||
    (ts.isBinaryExpression(current) && joins.includes(current.operatorToken.kind)) ||
    ts.isParenthesizedExpression(current)
  ) {
    node = current
    current = current.parent
  }
  return ts.isJsxExpression(current) && !ts.isJsxAttribute(current.parent)
}

function attributeOf(node: ts.Node): string | null {
  const parent = node.parent
  if (ts.isJsxAttribute(parent)) return parent.name.getText()
  if (ts.isJsxExpression(parent) && ts.isJsxAttribute(parent.parent))
    return parent.parent.name.getText()
  return null
}

function findTexts(file: string, text = sources[file] ?? ''): string[] {
  const source = ts.createSourceFile(
    file,
    text,
    ts.ScriptTarget.Latest,
    true,
    file.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS,
  )
  const found: string[] = []
  const report = (node: ts.Node, text: string) => {
    const { line } = source.getLineAndCharacterOfPosition(node.getStart())
    found.push(`${file}:${line + 1} ${text.trim().replace(/\s+/g, ' ')}`)
  }

  const visit = (node: ts.Node) => {
    if (ts.isJsxText(node) && hasWords(node.text) && !sameInEveryLanguage.has(node.text.trim())) {
      report(node, node.text)
    }
    if (ts.isStringLiteral(node) && hasWords(node.text) && !sameInEveryLanguage.has(node.text)) {
      const attribute = attributeOf(node)
      if ((attribute && shownAttributes.has(attribute)) || inJsxContent(node))
        report(node, node.text)
      const call = node.parent
      if (
        ts.isCallExpression(call) &&
        /^toast(\.\w+)?$/.test(call.expression.getText()) &&
        call.arguments[0] === node
      ) {
        report(node, node.text)
      }
    }
    if (ts.isTemplateExpression(node) && hasWords(staticText(node))) {
      const attribute = attributeOf(node)
      const inJsx =
        (attribute !== null && !codeAttributes.has(attribute)) ||
        (ts.isJsxExpression(node.parent) && !ts.isJsxAttribute(node.parent.parent))
      if (inJsx && !/^\//.test(node.head.text)) report(node, node.getText())
    }
    ts.forEachChild(node, visit)
  }
  visit(source)
  return found
}

describe('screen texts (croatian-language.md FR-9)', () => {
  it('come from texts files, never written into a component', () => {
    const found = sourceFiles().flatMap((file) => findTexts(file))
    expect(sourceFiles().length).toBeGreaterThan(100)
    expect(found, 'Move these texts into a texts file, in English and Croatian').toEqual([])
  })

  it('finds a text written into a component', () => {
    const probe = [
      'export const A = () => (',
      '  <p title="Hello there" aria-label={`Show ${x}`}>',
      '    Loading…',
      '    <span className={`a ${b}`}>{`${n} paid`}</span>',
      "    {primary ? ' (primary)' : ''}",
      '  </p>',
      ')',
      'toast.success("Saved.")',
    ].join('\n')
    expect(findTexts('probe.tsx', probe).map((f) => f.split(' ')[0])).toEqual([
      'probe.tsx:2',
      'probe.tsx:2',
      'probe.tsx:3',
      'probe.tsx:4',
      'probe.tsx:5',
      'probe.tsx:8',
    ])
  })
})
