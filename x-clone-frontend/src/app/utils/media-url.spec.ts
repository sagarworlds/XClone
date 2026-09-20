import { IMAGE_TYPES, MAX_IMAGE_BYTES, MAX_IMAGES, mediaSrc } from './media-url';

describe('mediaSrc', () => {
  const name = '0123456789abcdef0123456789abcdef';

  it.each(['png', 'jpg', 'gif', 'webp'])('turns an uploaded .%s into an address on the API', (extension) => {
    expect(mediaSrc(`/uploads/${name}.${extension}`)).toBe(`http://localhost:5168/uploads/${name}.${extension}`);
  });

  it.each([
    ['a picture on another site', `https://evil.example/uploads/${name}.png`],
    ['a full address of our own', `http://localhost:5168/uploads/${name}.png`],
    ['a protocol-relative address', `//evil.example/uploads/${name}.png`],
    ['a script', 'javascript:alert(1)'],
    ['inline data', 'data:image/png;base64,AAAA'],
    ['a path outside the folder', `/uploads/../${name}.png`],
    ['a sub folder', `/uploads/x/${name}.png`],
    ['a name that is not ours', '/uploads/holiday.png'],
    ['capital letters', `/uploads/${name.toUpperCase()}.png`],
    ['a name that is too short', `/uploads/${name.slice(1)}.png`],
    ['a name that is too long', `/uploads/${name}0.png`],
    ['a name with a non-hex letter', `/uploads/${name.slice(1)}g.png`],
    ['an SVG', `/uploads/${name}.svg`],
    ['a query', `/uploads/${name}.png?x=1`],
    ['a fragment', `/uploads/${name}.png#x`],
    ['a trailing slash', `/uploads/${name}.png/`],
    ['a line break at the end', `/uploads/${name}.png\n`],
    ['nothing after the folder', '/uploads/'],
    ['an empty string', ''],
  ])('leaves out %s', (_label, url) => {
    expect(mediaSrc(url)).toBeNull();
  });
});

describe('the limits on images', () => {
  it('match what the API accepts', () => {
    expect(IMAGE_TYPES).toEqual(['image/png', 'image/jpeg', 'image/gif', 'image/webp']);
    expect(MAX_IMAGE_BYTES).toBe(5 * 1024 * 1024);
    expect(MAX_IMAGES).toBe(4);
  });
});
