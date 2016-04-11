from PIL import Image

import pydot
from reportlab.pdfgen import canvas
from reportlab.lib.pagesizes import letter

from lxml import etree


nodeNames = []

def getNodeName():
    c = len(nodeNames)
    name = "n%04d" % c
    nodeNames.append(name)
    return name

def findContinuousSpans(pixelList):
    pixelList.sort()
    spans = []
    while pixelList:
        startPixel = pixelList[0]
        endPixel = startPixel
        while endPixel in pixelList:
            pixelList.remove(endPixel)
            endPixel += 1            
        spans.append((startPixel, endPixel-1))
    return spans


def spanIsContainedInSpanList(span, spanList):
    for targetSpan in spanList:
        if (span[0] >= targetSpan[0] and
            span[0] <= targetSpan[1] and
            span[1] >= targetSpan[0] and
            span[1] <= targetSpan[1]):
            return True
    return False

def makeRectangles(pixelList):
    rectangles = []
    
    while True:
        scanlinesByX = {}
        for x,y in pixelList:
            oldScanLine = scanlinesByX.get(x,[])
            oldScanLine.append(y)
            scanlinesByX[x]=oldScanLine

        if not scanlinesByX:
            return rectangles
        
        for x,pixels in scanlinesByX.iteritems():
            scanlinesByX[x] = findContinuousSpans(pixels)

        xvals = scanlinesByX.keys()
        xvals.sort()

        startX = xvals[0]
        span = scanlinesByX[startX][0]
        #print "first span: ", startX, span
        endX = startX
        while True:
            endX += 1
            if endX not in scanlinesByX:
                break
            endSpans = scanlinesByX[endX]
            if not spanIsContainedInSpanList(span, endSpans):
                break
        endX -= 1
        #print "removing ", startX, endX, span
        rectangles.append((startX, endX+1, span[0], span[1]+1))
        for x in range(startX, endX+1):
            for y in range(span[0], span[1]+1):
                pixelList.remove((x,y))

def findPartitioningEdge(edgeList):
    # TODO make this smarter
    return edgeList[0]

class Node:
    def __init__(self, edge):
        self.edge = edge
        self.frontNode = None
        self.backNode = None
        self.name = getNodeName()

    def __str__(self):
        return "(Node {3} {0} F:{1} B:{2})".format(self.edge, self.frontNode, self.backNode, self.name)

def splitEdgesByEdge(edgeList, edge):
    frontEdges = []
    backEdges = []

    x1, y1, x2, y2, c = edge
    print edge
    ax = x2-x1
    ay = y2-y1

    print "ax,ay", ax,ay
        
    nx = -ay
    ny = ax
    print "nx, ny", nx, ny

    sx = x1
    sy = y1

    for testEdge in edgeList:

        # So, we've got a point at sx,sy, and a normal to our edge
        # going forward in the direction nx, ny. We want to find the
        # projection of our test points along that direction.

        # The orthogonal projection of vector v onto a line spanned by
        # a nonzero vector s is ((v dot s) / (s dot s)) times s

        # So, we can just find that scalar and use that to categorize.

        scalars = []
        tx1, ty1, tx2, ty2, tc = testEdge
        for tx, ty in [(tx1, ty1), (tx2, ty2)]:
            vx = tx - sx
            vy = ty - sy

            scalar = (vx*nx + vy*ny) / float(nx*nx + ny*ny)
            scalars.append(scalar)

        if scalars[0] >= 0 and scalars[1] >= 0:
            frontEdges.append(testEdge)
        elif scalars[0] <= 0 and scalars[1] <= 0:
            backEdges.append(testEdge)
        elif scalars[0] == 0 and scalars[1] == 0:
            # interesting - we should probably handle this by putting
            # it in our own node, but screw it, it'll go into the front.
            frontEdges.append(testEdge)
        else:
            """
            0 = n dot (t1 + k*[t2-t1] - s)
            0 = nx * (t1x + k*dx - sx) + ny * (t1y + k * dy - sy)
            """

            print "Splitting"
            print "S:",sx,sy
            print "N:", nx, ny
            print "T1:", tx1, ty1
            print "T2:", tx2, ty2

            dx = tx2-tx1
            dy = ty2-ty1

            if nx == 0:
                assert (dy != 0)
                """
                0 = ny * (t1y + k * dy - sy)
                sy - t1y = k * dy
                k = (sy - t1y) / dy
                """
                k = (sy - ty1) / float(dy)
            elif ny == 0:
                assert (dx != 0)
                k = (sx - tx1) / float(dx)
            else:
                # so much work
                assert(False)

            splitx = k*dx + tx1
            splity = k*dy + ty1
            
            edge1 = (tx1, ty1, splitx, splity, tc)
            edge2 = (splitx, splity, tx2, ty2, tc)
            
            if scalars[0] > 0:
                frontEdges.append(edge1)
                backEdges.append(edge2)
            else:
                backEdges.append(edge1)
                frontEdges.append(edge2)
            
                
    return frontEdges,backEdges        
                
def makePartition(edgeList, graph):
    print "making partition", edgeList
    if not edgeList:
        return None
    rootEdge = findPartitioningEdge(edgeList)
    edgeList.remove(rootEdge)
    print "removed ", rootEdge
    rootNode = Node(rootEdge)
    frontEdges,backEdges = splitEdgesByEdge(edgeList, rootEdge)
    print "f,b:", frontEdges, backEdges
    rootNode.frontNode = makePartition(frontEdges, graph)
    rootNode.backNode = makePartition(backEdges, graph)
    if graph:
        rn = pydot.Node(rootNode.name)
        graph.add_node(rn)
        if rootNode.frontNode:
            fn = pydot.Node(rootNode.frontNode.name)
            graph.add_node(fn)
            graph.add_edge(pydot.Edge(rn,fn))
        if rootNode.backNode:
            bn = pydot.Node(rootNode.backNode.name)
            graph.add_node(bn)
            graph.add_edge(pydot.Edge(rn,bn))
    
    return rootNode

def makeColorSubElement(xmlNode, color):
    colorElem = etree.SubElement(xmlNode, "RgbColor")
    colorElem.set('Red', str(color[0]))
    colorElem.set('Green', str(color[1]))
    colorElem.set('Blue', str(color[2]))

def processFile(fn, graphOut, mapOut, xmlFilenameOut):
    im = Image.open(fn)
    
    if graphOut:
        graph = pydot.Dot(graph_type="graph")
    else:
        graph = None
        
    pixelsByColor = {}

    root = None
    rectList = None
    
    if xmlFilenameOut:
        root = etree.Element("City")
        rectList = etree.SubElement(root, "Rects")
    
    for x in range(im.width):
        for y in range(im.height):
            r,g,b,a = im.getpixel((x,y))
            if a > 0:
                rgb = (r,g,b)
                xy = (x,y)
                oldPixelList = pixelsByColor.get(rgb, [])
                oldPixelList.append(xy)
                pixelsByColor[rgb] = oldPixelList

    edgeList = []
    for color,pixelList in pixelsByColor.iteritems():
        rects = makeRectangles(pixelList)

        for r in rects:
            x1, x2, y1, y2 = r
            edgeList.append((x1,y1,x1,y2,color))
            edgeList.append((x1,y2,x2,y2,color))
            edgeList.append((x2,y2,x2,y1,color))
            edgeList.append((x2,y1,x1,y1,color))
            if root is not None:
                rectElem = etree.SubElement(rectList, "BdgRect")
                rectElem.set('Left', str(x1))
                rectElem.set('Right', str(x2))
                rectElem.set('Bottom', str(y1))
                rectElem.set('Top', str(y2))
                makeColorSubElement(rectElem, color)

    print edgeList

    partition = makePartition(edgeList, graph)
    if graph:
        graph.write_pdf(graphOut)
    if mapOut:
        w,h = letter
        landscape = (max(w,h), min(w,h))

        c = canvas.Canvas(mapOut, pagesize = landscape)
        makeMap(partition, c)
        c.showPage()
        c.save()

    if root is not None:
        bspElem = etree.SubElement(root, "BspNodes")
        addNodeToXML(bspElem, partition)
        
    if root is not None:
        tree = etree.ElementTree(root)
        prettyString = etree.tostring(tree, pretty_print=True)
        with open(xmlFilenameOut, "wt") as f:
            f.write(prettyString)
    
    return partition

def addNodeToXML(xmlElement, node):
    if node is None:
        return

    subElement=etree.SubElement(xmlElement, "BspNode")
    subElement.set('edge', `node.edge`)
    subElement.set('Name', node.name)
    x1,x2,y1,y2, color = node.edge
    makeColorSubElement(subElement, color)
    
    if node.frontNode is not None:
        frontElement = etree.SubElement(subElement, "Front")
        addNodeToXML(frontElement, node.frontNode)
    if node.backNode is not None:
        backElement = etree.SubElement(subElement, "Back")
        addNodeToXML(backElement, node.backNode)

def makeMap(rootNode, c):
    if not rootNode:
        return

    w,h = letter
    h= min(w,h)

    x1, y1, x2, y2, color = rootNode.edge
    
    red,green,blue = color
    c.setStrokeColorRGB(red/255.0, green/ 255.0, blue/255.0)
    c.line(x1, h-y1, x2, h-y2)

    makeMap(rootNode.frontNode, c)
    makeMap(rootNode.backNode, c)
        

#print "test"
#print processFile("blueboxtest.png", None, None)
#print
#print "exits"        
#print processFile("autoduel-bos-exits.png", None, None)
#print
print "buildings"
processFile("autoduel-bos-buildings.png", "building-bsp.pdf", "building-map.pdf", "bos-buildings.xml")

        
