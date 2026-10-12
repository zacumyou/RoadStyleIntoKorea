import catalog from './text-catalog.json';
export type LocalText=(key:string)=>string;
type Translate=(key:string,fallback?:string)=>string|null|undefined;
// Game lookups allow I18NEveryWhere to supply any locale; native Korean/English remain independent.
export function localize(translate:Translate,ko:boolean):LocalText {
 const fallback:Record<string,string>=ko?catalog.ko:catalog.en;
 return key=>translate(key,fallback[key]||key)||fallback[key]||key;
}
