export function signPlacement(anchor:{left:number;right:number;top:number;bottom:number},width:number,height:number,vw:number,vh:number){
 const gap=12,availableWidth=Math.max(1,vw-2*gap),w=Math.min(width,availableWidth);
 const room=Math.max(1,vh-2*gap),h=Math.min(height,room);
 let x:number,y:number,maxHeight=h;
 if(anchor.left-gap>=w){x=anchor.left-gap-w;y=anchor.top;}
 else if(vw-anchor.right-gap>=w){x=anchor.right+gap;y=anchor.top;}
 else{
  const above=Math.max(0,anchor.top-2*gap),below=Math.max(0,vh-anchor.bottom-2*gap);
  x=anchor.right-w;
  if(Math.max(above,below)>=Math.min(h,180)){
   const up=above>below;maxHeight=Math.min(h,up?above:below);y=up?anchor.top-gap-Math.min(h,maxHeight):anchor.bottom+gap;
  }else{y=gap;maxHeight=h;} // Tall menus: overlap rather than put choices off screen.
 }
 return {left:Math.max(gap,Math.min(vw-gap-w,x)),top:Math.max(gap,Math.min(vh-gap-Math.min(h,maxHeight),y)),maxHeight,maxWidth:availableWidth};
}
