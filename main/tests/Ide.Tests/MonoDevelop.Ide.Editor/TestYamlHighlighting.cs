//
// SyntaxHighlightingTest.cs
//
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
//
// Copyright (c) 2016 Microsoft
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

using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Ide.Editor
{
	[Ignore("Fixme")]
	[TestFixture]
	class TestYamlHighlighting : IdeTestBase
	{
		[Test]
		public void TestComplexHighlighting ()
		{
			SyntaxHighlightingTest.RunSublimeHighlightingTest (yamlSyntax,
@"# SYNTAX TEST ""Packages/YAML/YAML.sublime-syntax""
^ source.yaml comment


##############################################################################
## Comments
# http://www.yaml.org/spec/1.2/spec.html#comment//

# comment
^ punctuation.definition.comment.line.number-sign
  ^ comment.line.number-sign


##############################################################################
## Document markers

---
^ entity.other.document.begin
  ^ entity.other.document.begin

...
^ entity.other.document.end
  ^ entity.other.document.end"
			);

		}


		const string yamlSyntax = @"%YAML 1.2
# The MIT License (MIT)
#
# Copyright (c) 2015 FichteFoll <fichtefoll2@googlemail.com>
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the ""Software""), to
# deal in the Software without restriction, including without limitation the
# rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
# sell copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in
# all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
# FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
# IN THE SOFTWARE.
#
#
# This syntax definition
# is based on http://yaml.org/spec/1.2/spec.html,
# which also serves as a base for variables.
# References have been included where appropriate.
#
# Acknowledgements:
# - Most indentation is not checked,
#   except for block scalars,
#   where it is also not verified
#   (i.e. highlights even if less indentation used than required).
# - Properties are sometimes incorrectly highlighted
#   for nested block collections (`- !!seq -`).
---
name: YAML
file_extensions:
  - yaml
  - yml
  - sublime-syntax
first_line_match: ^%YAML( ?1.\d+)?
scope: source.yaml

##############################################################################

variables:
  # General
  s_sep: '[ \t]+'
  c_indicator: '[-?:,\[\]{}#&*!|>''""%@`]'
  c_flow_indicator: '[\[\]{},]'
  ns_word_char: '[0-9A-Za-z\-]'
  ns_uri_char: '(?x: %\p{XDigit}{2} | [0-9A-Za-z\-#;/?:@&=+$,_.!~*''()\[\]] )'

  # Tag stuff
  c_tag_handle: (?:!(?:{{ns_word_char}}*!)?)
  ns_tag_char: '(?x: %\p{XDigit}{2} | [0-9A-Za-z\-#;/?:@&=+$_.~*''()] )'
  ns_tag_prefix: |-
    (?x:
        !              {{ns_uri_char}}*
      | (?![,!\[\]{}]) {{ns_uri_char}}+
    )
  c_ns_tag_property: |-
    (?x:
        ! < {{ns_uri_char}}+ >
      | {{c_tag_handle}} {{ns_tag_char}}+
      | !
    )

  # Anchor & Alias
  ns_anchor_char: '[^\s\[\]/{/},]'
  ns_anchor_name: '{{ns_anchor_char}}+'

  # double-quoted scalar
  c_ns_esc_char: \\([0abtnvfre ""/\\N_Lp]|x\d\d|u\d{4}|U\d{8})

  # plain scalar begin and end patterns
  ns_plain_first_plain_in: |- # c=plain-in
    (?x:
        [^\s{{c_indicator}}]
      | [?:-] [^\s{{c_flow_indicator}}]
    )

  ns_plain_first_plain_out: |- # c=plain-out
    (?x:
        [^\s{{c_indicator}}]
      | [?:-] \S
    )

  _flow_scalar_end_plain_in: |- # kind of the negation of nb-ns-plain-in-line(c) c=plain-in
    (?x:
      (?=
          \s* $
        | \s+ \#
        | \s* : \s
        | \s* : {{c_flow_indicator}}
        | {{c_flow_indicator}}
      )
    )

  _flow_scalar_end_plain_out: |- # kind of the negation of nb-ns-plain-in-line(c) c=plain-out
    (?x:
      (?=
          \s* $
        | \s+ \#
        | \s* : \s
      )
    )

  # patterns for plain scalars of implicit different types
  # (for the Core Schema: http://www.yaml.org/spec/1.2/spec.html#schema/core/)
  _type_null: (?:null|Null|NULL|~) # http://yaml.org/type/null.html
  _type_bool: |- # http://yaml.org/type/bool.html
    (?x:
       y|Y|yes|Yes|YES|n|N|no|No|NO
      |true|True|TRUE|false|False|FALSE
      |on|On|ON|off|Off|OFF
    )
  _type_int: |- # http://yaml.org/type/int.html
    (?x:
        [-+]? 0b [0-1_]+
      | [-+]? 0  [0-7_]+
      | [-+]? (?: 0|[1-9][0-9_]*)
      | [-+]? 0x [0-9a-fA-F_]+
      | [-+]? [1-9] [0-9_]* (?: :[0-5]?[0-9])+
    )
  _type_float: |- # http://yaml.org/type/float.html
    (?x:
        [-+]? (?: [0-9] [0-9_]*)? \. [0-9.]* (?: [eE] [-+] [0-9]+)?
      | [-+]? [0-9] [0-9_]* (?: :[0-5]?[0-9])+ \. [0-9_]*
      | [-+]? \. (?: inf|Inf|INF)
      |       \. (?: nan|NaN|NAN)
    )
  _type_timestamp: |- # http://yaml.org/type/timestamp.html
    (?x:
        \d{4} - \d{2} - \d{2}
      | \d{4}
        - \d{1,2}
        - \d{1,2}
        (?: [Tt] | [ \t]+) \d{1,2}
        : \d{2}
        : \d{2}
        (?: \.\d*)?
        (?:
            (?:[ \t]*) Z
          | [-+] \d{1,2} (?: :\d{1,2})?
        )?
    )
  _type_value: '=' # http://yaml.org/type/value.html
  _type_merge: '<<' # http://yaml.org/type/merge.html

  _type_all: |-
    (?x:
        ({{_type_null}})
      | ({{_type_bool}})
      | ({{_type_int}})
      | ({{_type_float}})
      | ({{_type_timestamp}})
      | ({{_type_value}})
      | ({{_type_merge}})
     )

##############################################################################

contexts:
  prototype:
    - include: comment
    - include: property

  main:
    - include: directive
    - match: ^---
      scope: entity.other.document.begin.yaml
    - match: ^\.{3}
      scope: entity.other.document.end.yaml
    - include: node
    # Use this for debugging. It should not match anything.
    # - match: \S
    #   scope: invalid.illegal.unmatched.yaml

  node:
    - include: block-node

  flow-node:
    # http://yaml.org/spec/1.2/spec.html#style/flow/
    # ns-flow-yaml-node(n,c)
    - include: flow-alias
    - include: flow-collection
    - include: flow-scalar

  flow-scalar:
    # http://yaml.org/spec/1.2/spec.html#style/flow/scalar
    - include: flow-scalar-double-quoted
    - include: flow-scalar-single-quoted
    - include: flow-scalar-plain-in

  flow-collection:
    # http://yaml.org/spec/1.2/spec.html#style/flow/collection
    - include: flow-sequence
    - include: flow-mapping

  block-node:
    # http://yaml.org/spec/1.2/spec.html#style/block/
    - include: block-scalar
    - include: block-collection
    - include: flow-scalar-plain-out  # needs higher priority than flow-node, which includes flow-scalar-plain-in
    - include: flow-node

  block-collection:
    # http://yaml.org/spec/1.2/spec.html#style/block/collection
    - include: block-sequence
    - include: block-mapping


######################################

  directive:
    # http://yaml.org/spec/1.2/spec.html#directive//
    - match: ^%
      scope: punctuation.definition.directive.begin.yaml
      push:
        - meta_scope: meta.directive.yaml
        # %YAML directive
        # http://yaml.org/spec/1.2/spec.html#directive/YAML/
        - match: (YAML)[ \t]+(\d+\.\d+)
          captures:
            1: keyword.other.directive.yaml.yaml
            2: constant.numeric.yaml-version.yaml
          set: directive-finish
        # %TAG directive
        # http://yaml.org/spec/1.2/spec.html#directive/TAG/
        - match: |
            (?x)
            (TAG)
            (?:[ \t]+
             ({{c_tag_handle}})
             (?:[ \t]+ ({{ns_tag_prefix}}) )?
            )?
          # handle and prefix are optional for when typing
          captures:
            1: keyword.other.directive.tag.yaml
            2: storage.type.tag-handle.yaml
            3: support.type.tag-prefix.yaml
          set: directive-finish
        # Any other directive
        # http://yaml.org/spec/1.2/spec.html#directive/reserved/
        - match: (?x) (\w+) (?:[ \t]+ (\w+) (?:[ \t]+ (\w+))? )?
          # name and parameter are optional for when typing
          captures:
            1: support.other.directive.reserved.yaml
            2: string.unquoted.directive-name.yaml
            3: string.unquoted.directive-parameter.yaml
          set: directive-finish
        - match: ''
          set: directive-finish

  directive-finish:
    - match: (?=$|[ \t]+($|#))
      pop: true
    - match: \S+
      scope: invalid.illegal.unrecognized.yaml

  property:
    # http://yaml.org/spec/1.2/spec.html#node/property/
    - match: (?=!|&)
      push:
      - meta_scope: meta.property.yaml
      # &Anchor
      # http://yaml.org/spec/1.2/spec.html#&%20anchor//
      - match: (&)({{ns_anchor_name}})(\S+)?
        captures:
          1: keyword.control.property.anchor.yaml punctuation.anchor.yaml
          2: entity.name.other.anchor.yaml
          3: invalid.illegal.character.anchor.yaml
        pop: true
      # !Tag Handle
      # http://yaml.org/spec/1.2/spec.html#tag/property/
      - match: '{{c_ns_tag_property}}(?=\ |\t|$)'
        scope: storage.type.tag-handle.yaml
        pop: true
      - match: \S+
        scope: invalid.illegal.tag-handle.yaml
        pop: true

  flow-alias:
    # http://yaml.org/spec/1.2/spec.html#alias//
    - match: (\*)({{ns_anchor_name}})([^\s\]},]\S*)?
      captures:
        1: keyword.control.flow.alias.yaml punctuation.alias.yaml
        2: variable.other.alias.yaml
        3: invalid.illegal.character.anchor.yaml

  flow-scalar-double-quoted:
    # http://yaml.org/spec/1.2/spec.html#style/flow/double-quoted
    # c-double-quoted(n,c)
    - match: '""'
      scope: punctuation.definition.string.begin.yaml
      push:
        # TODO consider scoping meaningful trailing whitespace for color
        # schemes with background color definitions.
        - meta_scope: string.quoted.double.yaml
          meta_include_prototype: false
        - match: '{{c_ns_esc_char}}'
          scope: constant.character.escape.double-quoted.yaml
        - match: \\\n
          scope: constant.character.escape.double-quoted.newline.yaml
        - match: '""'
          scope: punctuation.definition.string.end.yaml
          pop: true

  flow-scalar-single-quoted:
    # http://yaml.org/spec/1.2/spec.html#style/flow/single-quoted
    # c-single-quoted(n,c)
    - match: ""'""
      scope: punctuation.definition.string.begin.yaml
      push:
        - meta_scope: string.quoted.single.yaml
          meta_include_prototype: false
        - match: ""''""
          scope: constant.character.escape.single-quoted.yaml
        - match: ""'""
          scope: punctuation.definition.string.end.yaml
          pop: true

  flow-scalar-plain-in-implicit-type:
    - match: |
        (?x)
        {{_type_all}}
        {{_flow_scalar_end_plain_in}}
      captures: # &implicit_type_captures
        1: constant.language.null.yaml
        2: constant.language.boolean.yaml
        3: constant.numeric.integer.yaml
        4: constant.numeric.float.yaml
        5: constant.other.timestamp.yaml
        6: constant.language.value.yaml
        7: constant.language.merge.yaml

  flow-scalar-plain-out-implicit-type:
    - match: |
        (?x)
        {{_type_all}}
        {{_flow_scalar_end_plain_out}}
      captures: # *implicit_type_captures
        # Alias does not work, see https://github.com/SublimeTextIssues/Core/issues/967
        1: constant.language.null.yaml
        2: constant.language.boolean.yaml
        3: constant.numeric.integer.yaml
        4: constant.numeric.float.yaml
        5: constant.other.timestamp.yaml
        6: constant.language.value.yaml
        7: constant.language.merge.yaml

  flow-scalar-plain-out:
    # http://yaml.org/spec/1.2/spec.html#style/flow/plain
    # ns-plain(n,c) (c=flow-out, c=block-key)
    - include: flow-scalar-plain-out-implicit-type
    - match: '{{ns_plain_first_plain_out}}'
      push:
        - meta_scope: string.unquoted.plain.out.yaml
          meta_include_prototype: false
        - match: '{{_flow_scalar_end_plain_out}}'
          pop: true

  flow-scalar-plain-in:
    # http://yaml.org/spec/1.2/spec.html#style/flow/plain
    # ns-plain(n,c) (c=flow-in, c=flow-key)
    - include: flow-scalar-plain-in-implicit-type
    - match: '{{ns_plain_first_plain_in}}'
      push:
        - meta_scope: string.unquoted.plain.in.yaml
          meta_include_prototype: false
        - match: '{{_flow_scalar_end_plain_in}}'
          pop: true

  flow-sequence:
    # http://yaml.org/spec/1.2/spec.html#style/flow/sequence
    # c-flow-sequence(n,c)
    - match: \[
      scope: punctuation.definition.sequence.begin.yaml
      push:
        - meta_scope: meta.flow-sequence.yaml
        - match: \]
          scope: punctuation.definition.sequence.end.yaml
          pop: true
        - match: ','
          scope: punctuation.separator.sequence.yaml
        - include: flow-pair
        - include: flow-node

  flow-mapping:
    - match: \{
      scope: punctuation.definition.mapping.begin.yaml
      push:
        - meta_scope: meta.flow-mapping.yaml
        - match: \}
          scope: punctuation.definition.mapping.end.yaml
          pop: true
        - match: ','
          scope: punctuation.separator.mapping.yaml
        - include: flow-pair

  flow-pair:
    - match: \?
      scope: punctuation.definition.key-value.begin.yaml
      push:
        - meta_scope: meta.flow-pair.explicit.yaml
        - match: (?=[},\]]) # Empty mapping keys & values are allowed
          pop: true
        - include: flow-pair
        - include: flow-node
        - match: :(?=\s|$|{{c_flow_indicator}})
          scope: punctuation.separator.mapping.key-value.yaml
          set: flow-pair-value
    # Attempt to match plain-in scalars and highlight as ""entity.name.tag"",
    # if followed by a colon
    - match: |
        (?x)
        (?=
          {{ns_plain_first_plain_in}}
          (
              [^\s:{{c_flow_indicator}}]
            | : [^\s{{c_flow_indicator}}]
            | \s+ (?![#\s])
          )*
          \s*
          :\s
        )
      push:
        # TODO Use a merge type here and add ""pop: true"" and ""scope: entity.name.tag.yaml"";
        # https://github.com/SublimeTextIssues/Core/issues/966
        - meta_scope: meta.flow-pair.key.yaml
        - include: flow-scalar-plain-in-implicit-type
        - match: '{{_flow_scalar_end_plain_in}}'
          pop: true
        - match: '{{ns_plain_first_plain_in}}'
          set:
            - meta_scope: string.unquoted.plain.in.yaml entity.name.tag.yaml
              meta_include_prototype: false
            - match: '{{_flow_scalar_end_plain_in}}'
              pop: true
    - include: flow-node
    - match: :(?=\s|$|{{c_flow_indicator}}) # Empty mapping keys allowed
      scope: meta.flow-pair.yaml punctuation.separator.mapping.key-value.yaml
      push: flow-pair-value

  flow-pair-value:
    - meta_content_scope: meta.flow-pair.value.yaml
    - include: flow-node
    - match: (?=[},\]])
      pop: true

  block-scalar:
    # http://www.yaml.org/spec/1.2/spec.html#style/block/scalar
    # c-l+literal(n) | c-l+folded(n)
    - match: (?:(\|)|(>))([1-9])?([-+])?  # c-b-block-header(m,t)
      captures:
        1: punctuation.definition.block.scalar.literal.yaml
        2: punctuation.definition.block.scalar.folded.yaml
        3: constant.numeric.indentation-indicator.yaml
        4: support.other.chomping-indicator.yaml
      push:
        - meta_include_prototype: false
        - match: ^([ ]+)(?! )  # match first non-empty line to determine indentation level
          # note that we do not check if indentation is enough
          set:
            - meta_scope: string.unquoted.block.yaml
              meta_include_prototype: false
            - match: ^(?!^([ ]+)(?! )|\s*$)
              pop: true
        - match: ^(?=\S)  # the block is empty
          pop: true
        - include: comment  # include comments but not properties
        - match: .+
          scope: invalid.illegal.expected-comment-or-newline.yaml

  block-sequence:
    # http://www.yaml.org/spec/1.2/spec.html#style/block/sequence
    # l+block-sequence(n)
    - match: (-)\s  # ( |\t|$) # https://github.com/SublimeTextIssues/Core/issues/963
      captures:
        1: punctuation.definition.block.sequence.item.yaml

  block-mapping:
    # http://www.yaml.org/spec/1.2/spec.html#style/block/mapping
    # l+block-mapping(n)
    - include: block-pair

  block-pair:
    - match: \?
      scope: punctuation.definition.key-value.begin.yaml
      push:
        - meta_scope: meta.block-mapping.yaml
        - match: (?=\?) # Empty mapping keys & values are allowed
          pop: true
        - match: ^ *(:)
          captures:
            1: punctuation.separator.mapping.key-value.yaml
          pop: true
        - match: ':'
          scope: invalid.illegal.expected-newline.yaml
          pop: true
        - include: block-node
    # Attempt to match plain-out scalars and highlight as ""entity.name.tag"",
    # if followed by a colon
    - match: |
        (?x)
        (?=
          {{ns_plain_first_plain_out}}
          (
              [^\s:]
            | : \S
            | \s+ (?![#\s])
          )*
          \s*
          :\s
        )
      push:
        - include: flow-scalar-plain-out-implicit-type
        - match: '{{_flow_scalar_end_plain_out}}'
          pop: true
        - match: '{{ns_plain_first_plain_out}}'
          set:
            - meta_scope: string.unquoted.plain.out.yaml entity.name.tag.yaml
              meta_include_prototype: false
            - match: '{{_flow_scalar_end_plain_out}}'
              pop: true
    - match: :(?=\s|$)
      scope: punctuation.separator.mapping.key-value.yaml

  comment:
    # http://www.yaml.org/spec/1.2/spec.html#comment//
    - match: | # l-comment
        (?x)
        (?: ^ [ \t]* | [ \t]+ )
        (?:(\#) \p{Print}* )?
        (\n|\z)
      scope: comment.line.number-sign.yaml
      captures:
        1: punctuation.definition.comment.line.number-sign.yaml
...
";
	}
}
