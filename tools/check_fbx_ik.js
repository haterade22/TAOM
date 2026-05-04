#!/usr/bin/env node
// Detect IK joints / constraints in an FBX file.
// IK can be expressed as:
//   - Model nodes of type "IKEffector"
//   - Constraint nodes (FbxConstraint, type=IK)
//   - Custom properties on bones (P: "IKEnabled", etc.)
// Usage: node tools/check_fbx_ik.js <path-to.fbx>

const fs = require('fs');
const path = require('path');
const { parseBinary } = require('fbx-parser');

function findSection(nodes, name) {
  for (const n of nodes) if (n.name === name) return n;
  return null;
}

function main() {
  const fbxPath = process.argv[2];
  if (!fbxPath) {
    console.error('Usage: node check_fbx_ik.js <path>');
    process.exit(1);
  }
  const resolved = path.resolve(fbxPath);
  const buf = fs.readFileSync(resolved);
  const root = parseBinary(buf);

  const objects = findSection(root, 'Objects');
  if (!objects) { console.error('No Objects section'); process.exit(2); }

  // 1) Tally Model node types - looking for IKEffector specifically
  const modelTypeCount = {};
  const ikEffectors = [];
  const allBoneNames = [];
  for (const n of objects.nodes || []) {
    if (n.name === 'Model') {
      const [, fullName, type] = n.props;
      const t = String(type);
      modelTypeCount[t] = (modelTypeCount[t] || 0) + 1;
      const display = String(fullName).replace(/^Model::/, '').split(/ |\x00/)[0];
      if (t === 'IKEffector') ikEffectors.push(display);
      if (t === 'LimbNode' || t === 'Limb' || t === 'Null') allBoneNames.push(display);
    }
  }

  // 2) Look for any Constraint nodes
  const constraints = [];
  for (const n of objects.nodes || []) {
    if (n.name === 'Constraint' || n.name === 'FbxConstraint' || (n.name === 'Object' && n.props && /Constraint/i.test(String(n.props[2] || '')))) {
      constraints.push({ name: n.name, props: n.props });
    }
  }

  // 3) Walk all Model nodes for "P:" custom properties referencing IK
  const ikProperties = [];
  function walkProps(node, modelName) {
    if (!node.nodes) return;
    for (const child of node.nodes) {
      if (child.name === 'P' && child.props) {
        const pName = String(child.props[0] || '');
        if (/IK|Ik/.test(pName)) {
          ikProperties.push({ model: modelName, propName: pName, value: child.props.slice(4) });
        }
      }
      walkProps(child, modelName);
    }
  }
  for (const n of objects.nodes || []) {
    if (n.name === 'Model') {
      const [, fullName] = n.props;
      const display = String(fullName).replace(/^Model::/, '').split(/ |\x00/)[0];
      walkProps(n, display);
    }
  }

  // 4) Raw binary scan for IK-related strings (catches anything we missed)
  const ikStringMatches = new Set();
  const re = /\b(IK\w*|FbxIK\w*|FK_to_IK|IkChain\w*)\b/g;
  // FBX binary contains many ASCII identifiers; scan as latin-1
  const text = buf.toString('latin1');
  let m;
  while ((m = re.exec(text)) !== null) {
    if (m[0].length < 50) ikStringMatches.add(m[0]);
  }

  console.log(`\n=== ${path.basename(resolved)} (${buf.length.toLocaleString()} bytes) ===\n`);
  console.log('Model types:', JSON.stringify(modelTypeCount));
  console.log(`Bone-like models: ${allBoneNames.length}`);
  console.log(`\nIK Effectors (Model type=IKEffector): ${ikEffectors.length}`);
  if (ikEffectors.length) ikEffectors.forEach(n => console.log(`  - ${n}`));
  console.log(`\nConstraint nodes: ${constraints.length}`);
  if (constraints.length) constraints.forEach(c => console.log(`  - ${c.name}: ${JSON.stringify(c.props).slice(0, 200)}`));
  console.log(`\nIK-named custom properties on Models: ${ikProperties.length}`);
  if (ikProperties.length) {
    ikProperties.slice(0, 20).forEach(p => console.log(`  - [${p.model}] ${p.propName} = ${JSON.stringify(p.value).slice(0,120)}`));
    if (ikProperties.length > 20) console.log(`  ...and ${ikProperties.length - 20} more`);
  }
  console.log(`\nRaw binary "IK*" string matches: ${ikStringMatches.size} unique`);
  if (ikStringMatches.size) console.log('  ' + Array.from(ikStringMatches).slice(0, 30).join(', '));
}

main();
